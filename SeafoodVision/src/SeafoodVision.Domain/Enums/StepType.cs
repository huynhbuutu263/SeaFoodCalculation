namespace SeafoodVision.Domain.Enums;

/// <summary>
/// Identifies the OpenCV operation performed by an <c>InspectionStep</c>.
/// The integer values are persisted to the database — do not renumber existing values.
/// </summary>
public enum StepType : short
{
    // ── Color-space ────────────────────────────────────────────────
    /// <summary>Convert BGR frame to grayscale. No parameters.</summary>
    GrayConvert = 0,

    /// <summary>
    /// Convert to HSV and apply a hue/saturation/value range mask.
    /// Params: HMin, HMax, SMin, SMax, VMin, VMax (0–255).
    /// </summary>
    ColorFilter = 1,

    // ── Blur / Noise reduction ──────────────────────────────────────
    /// <summary>
    /// Gaussian blur.
    /// Params: KernelWidth (odd), KernelHeight (odd), SigmaX.
    /// </summary>
    GaussianBlur = 10,

    /// <summary>
    /// Median blur (good for salt-and-pepper noise).
    /// Params: KernelSize (odd).
    /// </summary>
    MedianBlur = 11,

    // ── Thresholding ────────────────────────────────────────────────
    /// <summary>
    /// Global threshold.
    /// Params: ThreshValue (0–255), MaxValue (0–255), ThreshType (Binary|BinaryInv|Otsu).
    /// </summary>
    Threshold = 20,

    /// <summary>
    /// Adaptive threshold (handles uneven lighting).
    /// Params: MaxValue, Method (Mean|Gaussian), ThreshType, BlockSize (odd), C.
    /// </summary>
    AdaptiveThreshold = 21,

    // ── Morphology ──────────────────────────────────────────────────
    /// <summary>
    /// Morphological operation (erode, dilate, open, close, …).
    /// Params: Operation (Erode|Dilate|Open|Close|Gradient|TopHat|BlackHat), KernelSize, Iterations.
    /// </summary>
    Morphology = 30,

    // ── Edge detection ──────────────────────────────────────────────
    /// <summary>
    /// Canny edge detector.
    /// Params: Threshold1, Threshold2, ApertureSize (3|5|7).
    /// </summary>
    Canny = 40,

    // ── Feature / Blob detection ────────────────────────────────────
    /// <summary>
    /// Find contours and filter by area / circularity / aspect ratio.
    /// Params: MinArea, MaxArea, MinCircularity, MaxCircularity, MinAspectRatio, MaxAspectRatio.
    /// </summary>
    ContourFilter = 50,

    /// <summary>
    /// SimpleBlobDetector.
    /// Params: MinArea, MaxArea, MinCircularity, FilterByColor, BlobColor.
    /// </summary>
    BlobDetector = 51,

    // ── Template matching ────────────────────────────────────────────
    /// <summary>
    /// Template matching (cv::matchTemplate).
    /// Params: TemplatePath, Method (CCoeffNormed|SqDiffNormed|…), MatchThreshold.
    /// </summary>
    TemplateMatcher = 60,

    // ── Defect detection ────────────────────────────────────────────
    /// <summary>
    /// Absolute-difference defect detector (compares ROI to a golden reference).
    /// Params: ReferencePath, Sensitivity (0–255), MinDefectArea.
    /// </summary>
    DefectDetector = 70
}
