using System;
using Unity.InferenceEngine;
using Unity.Mathematics;
using UnityEngine;

public class PoseManager : MonoBehaviour
{
    public PhoneWebcamView webcamView;
    public PosePreview posePreview;
    public ImagePreview imagePreview;
    public ModelAsset poseDetector;
    public ModelAsset poseLandmarker;
    public TextAsset anchorsCSV;
    public float scoreThreshold = 0.75f;

    const int k_NumAnchors = 2254;
    const int k_NumKeypoints = 33;
    const int k_DetectorInputSize = 224;
    const int k_LandmarkerInputSize = 256;

    float[,] m_Anchors;
    Worker m_PoseDetectorWorker;
    Worker m_PoseLandmarkerWorker;
    Tensor<float> m_DetectorInput;
    Tensor<float> m_LandmarkerInput;
    string m_LandmarkerOutputName;
    bool m_IsProcessingFrame;
    bool m_IsDestroying;

    void Start()
    {
        if (webcamView == null || posePreview == null || poseDetector == null || poseLandmarker == null || anchorsCSV == null)
        {
            Debug.LogError("PoseManager is missing one or more required references.");
            enabled = false;
            return;
        }

        m_Anchors = BlazeUtils.LoadAnchors(anchorsCSV.text, k_NumAnchors);

        var poseDetectorModel = ModelLoader.Load(poseDetector);
        var graph = new FunctionalGraph();
        var detectorInput = graph.AddInput(poseDetectorModel, 0);
        var detectorOutputs = Functional.Forward(poseDetectorModel, detectorInput);
        var filteredDetectorOutputs = BlazeUtils.ArgMaxFiltering(detectorOutputs[0], detectorOutputs[1]);
        poseDetectorModel = graph.Compile(filteredDetectorOutputs.Item1, filteredDetectorOutputs.Item2, filteredDetectorOutputs.Item3);
        m_PoseDetectorWorker = CreateWorker(poseDetectorModel, nameof(poseDetector));

        var poseLandmarkerModel = ModelLoader.Load(poseLandmarker);
        m_LandmarkerOutputName = poseLandmarkerModel.outputs[0].name;
        m_PoseLandmarkerWorker = CreateWorker(poseLandmarkerModel, nameof(poseLandmarker));

        m_DetectorInput = new Tensor<float>(new TensorShape(1, k_DetectorInputSize, k_DetectorInputSize, 3));
        m_LandmarkerInput = new Tensor<float>(new TensorShape(1, k_LandmarkerInputSize, k_LandmarkerInputSize, 3));
        posePreview.SetActive(false);
    }

    void Update()
    {
        if (m_IsDestroying || m_IsProcessingFrame)
            return;

        var webcamTexture = webcamView != null ? webcamView.WebcamTexture : null;
        if (webcamTexture == null || !webcamTexture.isPlaying || !webcamTexture.didUpdateThisFrame || webcamTexture.width <= 16)
            return;

        _ = ProcessFrameAsync(webcamTexture);

        // 여기서 webcam 프레임을 모델 입력으로 넘길 예정
    }
    async Awaitable ProcessFrameAsync(WebCamTexture webcamTexture)
    {
        m_IsProcessingFrame = true;

        try
        {
            if (imagePreview != null)
                imagePreview.SetTexture(webcamTexture);

            var textureWidth = webcamTexture.width;
            var textureHeight = webcamTexture.height;
            var maxSize = Mathf.Max(textureWidth, textureHeight);

            var detectorScale = maxSize / (float)k_DetectorInputSize;
            var detectorMatrix = BlazeUtils.mul(
                BlazeUtils.TranslationMatrix(0.5f * (new Vector2(textureWidth, textureHeight) + new Vector2(-maxSize, maxSize))),
                BlazeUtils.ScaleMatrix(new Vector2(detectorScale, -detectorScale)));

            BlazeUtils.SampleImageAffine(webcamTexture, m_DetectorInput, detectorMatrix);
            m_PoseDetectorWorker.Schedule(m_DetectorInput);

            var outputIdxAwaitable = (m_PoseDetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            var outputScoreAwaitable = (m_PoseDetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            var outputBoxAwaitable = (m_PoseDetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

            using var outputIdx = await outputIdxAwaitable;
            using var outputScore = await outputScoreAwaitable;
            using var outputBox = await outputBoxAwaitable;

            var scorePassesThreshold = outputScore[0] >= scoreThreshold;
            posePreview.SetActive(scorePassesThreshold);
            if (!scorePassesThreshold || m_IsDestroying)
                return;

            var selectedIndex = outputIdx[0];
            var anchorPosition = k_DetectorInputSize * new float2(m_Anchors[selectedIndex, 0], m_Anchors[selectedIndex, 1]);

            var centerImageSpace = BlazeUtils.mul(detectorMatrix, anchorPosition + new float2(outputBox[0, 0, 0], outputBox[0, 0, 1]));
            var topRightImageSpace = BlazeUtils.mul(detectorMatrix, anchorPosition + new float2(
                outputBox[0, 0, 0] + 0.5f * outputBox[0, 0, 2],
                outputBox[0, 0, 1] + 0.5f * outputBox[0, 0, 3]));

            var kp1ImageSpace = BlazeUtils.mul(detectorMatrix, anchorPosition + new float2(outputBox[0, 0, 4], outputBox[0, 0, 5]));
            var kp2ImageSpace = BlazeUtils.mul(detectorMatrix, anchorPosition + new float2(outputBox[0, 0, 6], outputBox[0, 0, 7]));
            var deltaImageSpace = kp2ImageSpace - kp1ImageSpace;
            var radius = 1.25f * math.length(deltaImageSpace);
            var theta = math.atan2(deltaImageSpace.y, deltaImageSpace.x);
            var origin = new float2(0.5f * k_LandmarkerInputSize, 0.5f * k_LandmarkerInputSize);
            var landmarkerScale = radius / (0.5f * k_LandmarkerInputSize);
            var landmarkerMatrix = BlazeUtils.mul(
                BlazeUtils.mul(
                    BlazeUtils.mul(
                        BlazeUtils.TranslationMatrix(kp1ImageSpace),
                        BlazeUtils.ScaleMatrix(new float2(landmarkerScale, -landmarkerScale))),
                    BlazeUtils.RotationMatrix(0.5f * Mathf.PI - theta)),
                BlazeUtils.TranslationMatrix(-origin));

            BlazeUtils.SampleImageAffine(webcamTexture, m_LandmarkerInput, landmarkerMatrix);

            var boxSize = 2f * (topRightImageSpace - centerImageSpace);
            posePreview.SetBoundingBox(true, ImageToWorld(centerImageSpace, textureWidth, textureHeight), boxSize / textureHeight);
            posePreview.SetBoundingCircle(true, ImageToWorld(kp1ImageSpace, textureWidth, textureHeight), radius / textureHeight);

            m_PoseLandmarkerWorker.Schedule(m_LandmarkerInput);
            var landmarksAwaitable = (m_PoseLandmarkerWorker.PeekOutput(m_LandmarkerOutputName) as Tensor<float>).ReadbackAndCloneAsync();
            using var landmarks = await landmarksAwaitable;

            if (m_IsDestroying)
                return;

            for (var i = 0; i < k_NumKeypoints; i++)
            {
                var positionImageSpace = BlazeUtils.mul(landmarkerMatrix, new float2(landmarks[5 * i], landmarks[5 * i + 1]));
                var visibility = landmarks[5 * i + 3];
                var presence = landmarks[5 * i + 4];
                var worldPosition = ImageToWorld(positionImageSpace, textureWidth, textureHeight) + new Vector3(0, 0, landmarks[5 * i + 2] / textureHeight);
                posePreview.SetKeypoint(i, visibility > 0.5f && presence > 0.5f, worldPosition);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            posePreview.SetActive(false);
        }
        finally
        {
            m_IsProcessingFrame = false;
        }
    }

    static Worker CreateWorker(Model model, string modelLabel)
    {
        try
        {
            return new Worker(model, BackendType.GPUCompute);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Falling back to CPU backend for {modelLabel}: {ex.Message}");
            return new Worker(model, BackendType.CPU);
        }
    }

    static Vector3 ImageToWorld(Vector2 position, float textureWidth, float textureHeight)
    {
        return (position - 0.5f * new Vector2(textureWidth, textureHeight)) / textureHeight;
    }

    void OnDestroy()
    {
        m_IsDestroying = true;
        m_PoseDetectorWorker?.Dispose();
        m_PoseLandmarkerWorker?.Dispose();
        m_DetectorInput?.Dispose();
        m_LandmarkerInput?.Dispose();
    }
}
