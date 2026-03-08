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
    DefectDetector = 70,

    // ── Region / Image manipulation ──────────────────────────────────
    /// <summary>
    /// Crop the image to a fixed rectangle (pixels).
    /// Params: X, Y, Width, Height.
    /// </summary>
    CropImage = 80,

    /// <summary>
    /// Subtract another step's output from the current image (absolute difference).
    /// Params: SecondaryInputStepOrder (1-based order of the secondary step).
    /// </summary>
    SubtractImage = 81,

    /// <summary>
    /// Bitwise AND of two binary masks (intersection of two regions).
    /// Params: SecondaryInputStepOrder (1-based order of the secondary step).
    /// </summary>
    IntersectionRegion = 82,

    /// <summary>
    /// Draw the bounding rectangle(s) of all white regions in a binary mask.
    /// Params: (none).
    /// </summary>
    GetRectangle = 83,

    /// <summary>
    /// Crops a region from the input image (same concept as adding an ROI, but inside a ROI
    /// pipeline). The cropped Mat is passed as a secondary input to downstream steps such as
    /// <see cref="TemplateMatcher"/> via <c>TemplateStepOrder</c>.
    /// Params: TemplateX, TemplateY, TemplateWidth, TemplateHeight.
    /// Output: the cropped region image (Mat).
    /// </summary>
    AddRegion = 84,

    /// <summary>
    /// Extracts geometric features from a binary mask (the largest contour):
    /// area, perimeter, circularity, orientation, bounding rectangle,
    /// minimum and maximum enclosing circles. Draws annotations on a colour overlay.
    /// Params: (none — all features are computed and overlaid automatically).
    /// </summary>
    RegionFeatures = 85,

    /// <summary>
    /// Applies a 2-D affine transformation (translation, rotation and uniform/non-uniform
    /// scale) to the source image.
    /// Params: TranslateX, TranslateY, Angle (degrees), ScaleX, ScaleY.
    /// </summary>
    AffineTransform = 86,

    /// <summary>
    /// Computes the minimum-area rotated bounding rectangle for every contour in a binary
    /// mask and draws it on a colour image (using <c>Cv2.BoxPoints</c> + <c>Cv2.Polylines</c>).
    /// The resulting image encodes the rotated-rect geometry so that downstream steps can
    /// consume it. Params: (none).
    /// </summary>
    SmallestRectangle = 87
}
