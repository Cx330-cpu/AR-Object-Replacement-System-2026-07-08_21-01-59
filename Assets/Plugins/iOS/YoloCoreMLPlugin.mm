#import <Foundation/Foundation.h>
#import <CoreML/CoreML.h>
#import <Vision/Vision.h>
#import <CoreVideo/CoreVideo.h>
#import <ImageIO/CGImageProperties.h>
#include <float.h>
#include <math.h>
#include <stdint.h>

struct ARORYoloCandidate
{
    float x;
    float y;
    float width;
    float height;
    int classId;
    float confidence;
    int maskCoefficientCount;
    float maskCoefficients[32];
    bool hasMaskBottomCenter;
    float maskBottomCenterX;
    float maskBottomCenterY;
    bool hasMaskCenter;
    float maskCenterX;
    float maskCenterY;
};

static VNCoreMLModel *gVisionModel = nil;
static BOOL gLoggedMissingModel = NO;

static float ARORClamp(float value, float minValue, float maxValue)
{
    return fmaxf(minValue, fminf(maxValue, value));
}

static float ARORSigmoid(float value)
{
    return 1.0f / (1.0f + expf(-value));
}

static float ARORIoU(const ARORYoloCandidate &left, const ARORYoloCandidate &right)
{
    const float leftX1 = left.x - left.width * 0.5f;
    const float leftY1 = left.y - left.height * 0.5f;
    const float leftX2 = left.x + left.width * 0.5f;
    const float leftY2 = left.y + left.height * 0.5f;
    const float rightX1 = right.x - right.width * 0.5f;
    const float rightY1 = right.y - right.height * 0.5f;
    const float rightX2 = right.x + right.width * 0.5f;
    const float rightY2 = right.y + right.height * 0.5f;

    const float intersectionX1 = fmaxf(leftX1, rightX1);
    const float intersectionY1 = fmaxf(leftY1, rightY1);
    const float intersectionX2 = fminf(leftX2, rightX2);
    const float intersectionY2 = fminf(leftY2, rightY2);
    const float intersectionWidth = fmaxf(0.0f, intersectionX2 - intersectionX1);
    const float intersectionHeight = fmaxf(0.0f, intersectionY2 - intersectionY1);
    const float intersectionArea = intersectionWidth * intersectionHeight;
    const float unionArea = left.width * left.height + right.width * right.height - intersectionArea;
    return unionArea > 0.0f ? intersectionArea / unionArea : 0.0f;
}

static BOOL ARORLoadYoloModel()
{
    if (gVisionModel != nil)
    {
        return YES;
    }

    NSBundle *bundle = [NSBundle mainBundle];
    NSURL *modelURL = [bundle URLForResource:@"yolov8n-seg" withExtension:@"mlpackage"];
    if (modelURL == nil)
    {
        modelURL = [bundle URLForResource:@"yolov8n" withExtension:@"mlpackage"];
    }
    if (modelURL == nil)
    {
        NSString *streamingAssetsPath = [[[bundle resourcePath] stringByAppendingPathComponent:@"Data"]
            stringByAppendingPathComponent:@"Raw"];
        NSString *streamingModelPath = [streamingAssetsPath stringByAppendingPathComponent:@"yolov8n-seg.mlpackage"];
        if ([[NSFileManager defaultManager] fileExistsAtPath:streamingModelPath])
        {
            modelURL = [NSURL fileURLWithPath:streamingModelPath isDirectory:YES];
        }
    }
    if (modelURL == nil)
    {
        NSString *streamingAssetsPath = [[[bundle resourcePath] stringByAppendingPathComponent:@"Data"]
            stringByAppendingPathComponent:@"Raw"];
        NSString *streamingModelPath = [streamingAssetsPath stringByAppendingPathComponent:@"yolov8n.mlpackage"];
        if ([[NSFileManager defaultManager] fileExistsAtPath:streamingModelPath])
        {
            modelURL = [NSURL fileURLWithPath:streamingModelPath isDirectory:YES];
        }
    }

    if (modelURL == nil)
    {
        modelURL = [bundle URLForResource:@"model" withExtension:@"mlmodel" subdirectory:@"yolov8n-seg.mlpackage/Data/com.apple.CoreML"];
    }
    if (modelURL == nil)
    {
        modelURL = [bundle URLForResource:@"model" withExtension:@"mlmodel" subdirectory:@"yolov8n.mlpackage/Data/com.apple.CoreML"];
    }
    if (modelURL == nil)
    {
        modelURL = [bundle URLForResource:@"model" withExtension:@"mlmodel" subdirectory:@"Data/Raw/yolov8n-seg.mlpackage/Data/com.apple.CoreML"];
    }
    if (modelURL == nil)
    {
        modelURL = [bundle URLForResource:@"model" withExtension:@"mlmodel" subdirectory:@"Data/Raw/yolov8n.mlpackage/Data/com.apple.CoreML"];
    }

    if (modelURL == nil)
    {
        if (!gLoggedMissingModel)
        {
            NSLog(@"[M1 YOLO] yolov8n-seg.mlpackage or yolov8n.mlpackage was not found. Checked app root and Data/Raw StreamingAssets.");
            gLoggedMissingModel = YES;
        }
        return NO;
    }

    NSError *error = nil;
    NSURL *compiledURL = nil;
    if ([[modelURL pathExtension] isEqualToString:@"mlpackage"] || [[modelURL pathExtension] isEqualToString:@"mlmodel"])
    {
        compiledURL = [MLModel compileModelAtURL:modelURL error:&error];
        if (compiledURL == nil || error != nil)
        {
            NSLog(@"[M1 YOLO] Failed to compile CoreML model: %@", error);
            return NO;
        }
    }
    else
    {
        compiledURL = modelURL;
    }

    MLModelConfiguration *configuration = [[MLModelConfiguration alloc] init];
    configuration.computeUnits = MLComputeUnitsAll;

    MLModel *model = [MLModel modelWithContentsOfURL:compiledURL configuration:configuration error:&error];
    if (model == nil || error != nil)
    {
        NSLog(@"[M1 YOLO] Failed to load CoreML model: %@", error);
        return NO;
    }

    gVisionModel = [VNCoreMLModel modelForMLModel:model error:&error];
    if (gVisionModel == nil || error != nil)
    {
        NSLog(@"[M1 YOLO] Failed to create Vision model: %@", error);
        return NO;
    }

    NSLog(@"[M1 YOLO] CoreML YOLO loaded with MLComputeUnitsAll: %@", modelURL.lastPathComponent);
    return YES;
}

static CVPixelBufferRef ARORCreatePixelBufferFromRGBA(const uint8_t *rgbaBytes, int width, int height)
{
    NSDictionary *attributes = @{
        (NSString *)kCVPixelBufferCGImageCompatibilityKey: @YES,
        (NSString *)kCVPixelBufferCGBitmapContextCompatibilityKey: @YES
    };

    CVPixelBufferRef pixelBuffer = nil;
    CVReturn result = CVPixelBufferCreate(
        kCFAllocatorDefault,
        width,
        height,
        kCVPixelFormatType_32BGRA,
        (__bridge CFDictionaryRef)attributes,
        &pixelBuffer);

    if (result != kCVReturnSuccess || pixelBuffer == nil)
    {
        return nil;
    }

    CVPixelBufferLockBaseAddress(pixelBuffer, 0);
    uint8_t *destination = (uint8_t *)CVPixelBufferGetBaseAddress(pixelBuffer);
    const size_t destinationStride = CVPixelBufferGetBytesPerRow(pixelBuffer);
    for (int y = 0; y < height; y++)
    {
        uint8_t *row = destination + y * destinationStride;
        const uint8_t *source = rgbaBytes + y * width * 4;
        for (int x = 0; x < width; x++)
        {
            row[x * 4 + 0] = source[x * 4 + 2];
            row[x * 4 + 1] = source[x * 4 + 1];
            row[x * 4 + 2] = source[x * 4 + 0];
            row[x * 4 + 3] = source[x * 4 + 3];
        }
    }
    CVPixelBufferUnlockBaseAddress(pixelBuffer, 0);

    return pixelBuffer;
}

static NSArray<VNCoreMLFeatureValueObservation *> *ARORRunVision(CVPixelBufferRef pixelBuffer)
{
    if (!ARORLoadYoloModel())
    {
        return nil;
    }

    __block NSArray<VNCoreMLFeatureValueObservation *> *observations = nil;
    VNCoreMLRequest *request = [[VNCoreMLRequest alloc] initWithModel:gVisionModel completionHandler:^(VNRequest *request, NSError *error) {
        if (error != nil)
        {
            NSLog(@"[M1 YOLO] Vision request failed: %@", error);
            return;
        }

        NSMutableArray<VNCoreMLFeatureValueObservation *> *featureObservations = [NSMutableArray array];
        for (VNObservation *observation in request.results)
        {
            if ([observation isKindOfClass:[VNCoreMLFeatureValueObservation class]])
            {
                [featureObservations addObject:(VNCoreMLFeatureValueObservation *)observation];
            }
        }
        observations = featureObservations;
    }];
    request.imageCropAndScaleOption = VNImageCropAndScaleOptionScaleFill;

    VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] initWithCVPixelBuffer:pixelBuffer orientation:kCGImagePropertyOrientationRight options:@{}];
    NSError *error = nil;
    BOOL success = [handler performRequests:@[request] error:&error];
    if (!success || error != nil)
    {
        NSLog(@"[M1 YOLO] Vision handler failed: %@", error);
        return nil;
    }

    return observations;
}

static BOOL ARORFindMaskBottomCenter(
    MLMultiArray *prototypeArray,
    const ARORYoloCandidate &candidate,
    int imageWidth,
    int imageHeight,
    float *bottomCenterX,
    float *bottomCenterY,
    float *centerX,
    float *centerY)
{
    if (prototypeArray == nil ||
        bottomCenterX == nullptr ||
        bottomCenterY == nullptr ||
        centerX == nullptr ||
        centerY == nullptr ||
        candidate.maskCoefficientCount <= 0 ||
        imageWidth <= 0 ||
        imageHeight <= 0)
    {
        return NO;
    }

    NSArray<NSNumber *> *shape = prototypeArray.shape;
    int protoChannels = candidate.maskCoefficientCount;
    int protoHeight = 160;
    int protoWidth = 160;
    if (shape.count >= 4)
    {
        protoChannels = fminf(candidate.maskCoefficientCount, shape[shape.count - 3].intValue);
        protoHeight = shape[shape.count - 2].intValue;
        protoWidth = shape[shape.count - 1].intValue;
    }
    else if (shape.count >= 3)
    {
        protoChannels = fminf(candidate.maskCoefficientCount, shape[shape.count - 3].intValue);
        protoHeight = shape[shape.count - 2].intValue;
        protoWidth = shape[shape.count - 1].intValue;
    }

    if (protoChannels <= 0 || protoHeight <= 0 || protoWidth <= 0 ||
        prototypeArray.count < protoChannels * protoHeight * protoWidth)
    {
        return NO;
    }

    float *prototypeValues = (float *)prototypeArray.dataPointer;
    const float boxLeft = ARORClamp(candidate.x - candidate.width * 0.5f, 0.0f, (float)imageWidth);
    const float boxTop = ARORClamp(candidate.y - candidate.height * 0.5f, 0.0f, (float)imageHeight);
    const float boxRight = ARORClamp(candidate.x + candidate.width * 0.5f, 0.0f, (float)imageWidth);
    const float boxBottom = ARORClamp(candidate.y + candidate.height * 0.5f, 0.0f, (float)imageHeight);

    const int minX = (int)ARORClamp(floorf(boxLeft * protoWidth / imageWidth), 0.0f, (float)(protoWidth - 1));
    const int maxX = (int)ARORClamp(ceilf(boxRight * protoWidth / imageWidth), 0.0f, (float)(protoWidth - 1));
    const int minY = (int)ARORClamp(floorf(boxTop * protoHeight / imageHeight), 0.0f, (float)(protoHeight - 1));
    const int maxY = (int)ARORClamp(ceilf(boxBottom * protoHeight / imageHeight), 0.0f, (float)(protoHeight - 1));

    int bottomMaskY = -1;
    float maskSumX = 0.0f;
    float maskSumY = 0.0f;
    int maskCount = 0;
    for (int y = minY; y <= maxY; y++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            float logit = 0.0f;
            const int pixelIndex = y * protoWidth + x;
            for (int channel = 0; channel < protoChannels; channel++)
            {
                logit += candidate.maskCoefficients[channel] *
                    prototypeValues[channel * protoHeight * protoWidth + pixelIndex];
            }

            if (ARORSigmoid(logit) >= 0.5f)
            {
                bottomMaskY = fmaxf(bottomMaskY, y);
                maskSumX += x;
                maskSumY += y;
                maskCount++;
            }
        }
    }

    if (bottomMaskY < 0 || maskCount <= 0)
    {
        return NO;
    }

    const int contactBandPixels = fmaxf(2, (maxY - minY + 1) * 0.08f);
    const int contactMinY = fmaxf(minY, bottomMaskY - contactBandPixels);
    float sumX = 0.0f;
    int count = 0;
    for (int y = contactMinY; y <= bottomMaskY; y++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            float logit = 0.0f;
            const int pixelIndex = y * protoWidth + x;
            for (int channel = 0; channel < protoChannels; channel++)
            {
                logit += candidate.maskCoefficients[channel] *
                    prototypeValues[channel * protoHeight * protoWidth + pixelIndex];
            }

            if (ARORSigmoid(logit) >= 0.5f)
            {
                sumX += x;
                count++;
            }
        }
    }

    if (count <= 0)
    {
        return NO;
    }

    *bottomCenterX = (sumX / count + 0.5f) * imageWidth / protoWidth;
    *bottomCenterY = (bottomMaskY + 0.5f) * imageHeight / protoHeight;
    *centerX = (maskSumX / maskCount + 0.5f) * imageWidth / protoWidth;
    *centerY = (maskSumY / maskCount + 0.5f) * imageHeight / protoHeight;
    return YES;
}

static BOOL ARORMatchesPreferredClass(int classId, int preferredClassId)
{
    if (preferredClassId < 0)
    {
        return YES;
    }

    if (classId == preferredClassId)
    {
        return YES;
    }

    // COCO cup family: bottle / wine glass / cup
    if (preferredClassId == 41)
    {
        return classId == 39 || classId == 40 || classId == 41;
    }

    // cell phone is often confused with remote
    if (preferredClassId == 67)
    {
        return classId == 67 || classId == 65;
    }

    return NO;
}

static float ARORClassScore(const float *values, int predictionCount, int channelCount, int index, int classId)
{
    if (values == nullptr || classId < 0 || 4 + classId >= channelCount)
    {
        return 0.0f;
    }

    return values[(4 + classId) * predictionCount + index];
}

static float ARORPreferredClassScore(
    const float *values,
    int predictionCount,
    int channelCount,
    int index,
    int preferredClassId,
    int *matchedClassId)
{
    if (preferredClassId < 0)
    {
        *matchedClassId = -1;
        return 0.0f;
    }

    int bestClass = preferredClassId;
    float bestScore = ARORClassScore(values, predictionCount, channelCount, index, preferredClassId);
    if (preferredClassId == 41)
    {
        const int family[] = {39, 40, 41};
        for (int i = 0; i < 3; i++)
        {
            const float score = ARORClassScore(values, predictionCount, channelCount, index, family[i]);
            if (score > bestScore)
            {
                bestScore = score;
                bestClass = family[i];
            }
        }
    }
    else if (preferredClassId == 67)
    {
        const float remoteScore = ARORClassScore(values, predictionCount, channelCount, index, 65);
        if (remoteScore > bestScore)
        {
            bestScore = remoteScore;
            bestClass = 65;
        }
    }

    *matchedClassId = bestClass;
    return bestScore;
}

static BOOL ARORBoxContainsPointWithMargin(
    const ARORYoloCandidate &candidate,
    float pointX,
    float pointY,
    float imageWidth,
    float imageHeight)
{
    const float pad = fmaxf(12.0f, 0.35f * fmaxf(candidate.width, candidate.height));
    const float extra = 0.06f * fmaxf(imageWidth, imageHeight);
    const float margin = fmaxf(pad, extra);
    const float x1 = candidate.x - candidate.width * 0.5f - margin;
    const float y1 = candidate.y - candidate.height * 0.5f - margin;
    const float x2 = candidate.x + candidate.width * 0.5f + margin;
    const float y2 = candidate.y + candidate.height * 0.5f + margin;
    return pointX >= x1 && pointX <= x2 && pointY >= y1 && pointY <= y2;
}

static BOOL ARORParseYoloOutput(
    MLMultiArray *array,
    MLMultiArray *prototypeArray,
    int imageWidth,
    int imageHeight,
    int screenWidth,
    int screenHeight,
    float confidenceThreshold,
    float iouThreshold,
    int preferredClassId,
    ARORYoloCandidate *selected)
{
    const int predictionCount = 8400;
    if (array == nil || array.count < 84 * predictionCount || array.count % predictionCount != 0)
    {
        return NO;
    }

    const int channelCount = (int)(array.count / predictionCount);
    const int classCount = 80;
    const int maskCoefficientCount = prototypeArray != nil ? fminf(32, fmaxf(0, channelCount - 4 - classCount)) : 0;
    float *values = (float *)array.dataPointer;
    NSMutableArray<NSValue *> *candidates = [NSMutableArray array];

    for (int index = 0; index < predictionCount; index++)
    {
        float bestConfidence = 0.0f;
        int bestClass = -1;
        for (int classIndex = 0; classIndex < classCount && 4 + classIndex < channelCount; classIndex++)
        {
            const float confidence = values[(4 + classIndex) * predictionCount + index];
            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestClass = classIndex;
            }
        }

        int preferredMatchClass = -1;
        const float preferredScore = ARORPreferredClassScore(
            values,
            predictionCount,
            channelCount,
            index,
            preferredClassId,
            &preferredMatchClass);
        if (preferredScore >= confidenceThreshold && preferredScore >= bestConfidence * 0.55f)
        {
            bestClass = preferredMatchClass;
            bestConfidence = fmaxf(bestConfidence, preferredScore);
        }

        if (bestConfidence < confidenceThreshold)
        {
            continue;
        }

        ARORYoloCandidate candidate;
        candidate.x = values[index] * imageWidth / 640.0f;
        candidate.y = values[predictionCount + index] * imageHeight / 640.0f;
        candidate.width = values[2 * predictionCount + index] * imageWidth / 640.0f;
        candidate.height = values[3 * predictionCount + index] * imageHeight / 640.0f;
        candidate.classId = bestClass;
        candidate.confidence = bestConfidence;
        candidate.maskCoefficientCount = maskCoefficientCount;
        candidate.hasMaskBottomCenter = false;
        candidate.maskBottomCenterX = 0.0f;
        candidate.maskBottomCenterY = 0.0f;
        candidate.hasMaskCenter = false;
        candidate.maskCenterX = 0.0f;
        candidate.maskCenterY = 0.0f;
        for (int coeff = 0; coeff < maskCoefficientCount; coeff++)
        {
            candidate.maskCoefficients[coeff] = values[(4 + classCount + coeff) * predictionCount + index];
        }

        if (candidate.width <= 0.5f || candidate.height <= 0.5f)
        {
            continue;
        }

        [candidates addObject:[NSValue valueWithBytes:&candidate objCType:@encode(ARORYoloCandidate)]];
    }

    [candidates sortUsingComparator:^NSComparisonResult(NSValue *leftValue, NSValue *rightValue) {
        ARORYoloCandidate left;
        ARORYoloCandidate right;
        [leftValue getValue:&left];
        [rightValue getValue:&right];
        if (left.confidence > right.confidence)
        {
            return NSOrderedAscending;
        }
        if (left.confidence < right.confidence)
        {
            return NSOrderedDescending;
        }
        return NSOrderedSame;
    }];

    NSMutableArray<NSValue *> *kept = [NSMutableArray array];
    for (NSValue *value in candidates)
    {
        ARORYoloCandidate candidate;
        [value getValue:&candidate];

        BOOL suppressed = NO;
        for (NSValue *keptValue in kept)
        {
            ARORYoloCandidate keptCandidate;
            [keptValue getValue:&keptCandidate];
            if (ARORIoU(candidate, keptCandidate) > iouThreshold)
            {
                suppressed = YES;
                break;
            }
        }

        if (!suppressed)
        {
            [kept addObject:value];
            if (kept.count >= 30)
            {
                break;
            }
        }
    }

    if (preferredClassId >= 0)
    {
        NSMutableArray<NSValue *> *preferredKept = [NSMutableArray array];
        for (NSValue *value in kept)
        {
            ARORYoloCandidate candidate;
            [value getValue:&candidate];
            if (ARORMatchesPreferredClass(candidate.classId, preferredClassId))
            {
                [preferredKept addObject:value];
            }
        }

        if (preferredKept.count > 0)
        {
            kept = preferredKept;
        }
        else
        {
            return NO;
        }
    }

    const float centerX = imageWidth * 0.5f;
    const float centerY = imageHeight * 0.5f;
    BOOL found = NO;
    ARORYoloCandidate best;
    best.confidence = 0.0f;

    for (NSValue *value in kept)
    {
        ARORYoloCandidate candidate;
        [value getValue:&candidate];
        if (ARORBoxContainsPointWithMargin(candidate, centerX, centerY, imageWidth, imageHeight) &&
            candidate.confidence > best.confidence)
        {
            best = candidate;
            found = YES;
        }
    }

    if (!found && kept.count > 0)
    {
        float bestDistance = FLT_MAX;
        for (NSValue *value in kept)
        {
            ARORYoloCandidate candidate;
            [value getValue:&candidate];
            const float dx = candidate.x - centerX;
            const float dy = candidate.y - centerY;
            const float distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
                found = YES;
            }
        }
    }

    if (!found)
    {
        return NO;
    }

    if (prototypeArray != nil && best.maskCoefficientCount > 0)
    {
        float maskX = 0.0f;
        float maskY = 0.0f;
        float centerX = 0.0f;
        float centerY = 0.0f;
        if (ARORFindMaskBottomCenter(prototypeArray, best, imageWidth, imageHeight, &maskX, &maskY, &centerX, &centerY))
        {
            best.hasMaskBottomCenter = true;
            best.maskBottomCenterX = ARORClamp(maskX * screenWidth / imageWidth, 0.0f, (float)screenWidth);
            best.maskBottomCenterY = ARORClamp(maskY * screenHeight / imageHeight, 0.0f, (float)screenHeight);
            best.hasMaskCenter = true;
            best.maskCenterX = ARORClamp(centerX * screenWidth / imageWidth, 0.0f, (float)screenWidth);
            best.maskCenterY = ARORClamp(centerY * screenHeight / imageHeight, 0.0f, (float)screenHeight);
        }
    }

    best.x = ARORClamp((best.x - best.width * 0.5f) * screenWidth / imageWidth, 0.0f, (float)screenWidth);
    best.y = ARORClamp((best.y - best.height * 0.5f) * screenHeight / imageHeight, 0.0f, (float)screenHeight);
    best.width = ARORClamp(best.width * screenWidth / imageWidth, 1.0f, (float)screenWidth - best.x);
    best.height = ARORClamp(best.height * screenHeight / imageHeight, 1.0f, (float)screenHeight - best.y);
    *selected = best;
    return YES;
}

extern "C"
{
    bool AROR_YoloIsAvailable()
    {
        return ARORLoadYoloModel();
    }

    bool AROR_YoloDetectCenterObject(
        const uint8_t *rgbaBytes,
        int byteCount,
        int imageWidth,
        int imageHeight,
        int screenWidth,
        int screenHeight,
        float confidenceThreshold,
        float iouThreshold,
        int preferredClassId,
        float *x,
        float *y,
        float *width,
        float *height,
        int *classId,
        float *confidence,
        int *hasMaskBottomCenter,
        float *maskBottomCenterX,
        float *maskBottomCenterY,
        int *hasMaskCenter,
        float *maskCenterX,
        float *maskCenterY)
    {
        if (rgbaBytes == nullptr || byteCount < imageWidth * imageHeight * 4 || imageWidth <= 0 || imageHeight <= 0)
        {
            return false;
        }

        CVPixelBufferRef pixelBuffer = ARORCreatePixelBufferFromRGBA(rgbaBytes, imageWidth, imageHeight);
        if (pixelBuffer == nil)
        {
            return false;
        }

        NSArray<VNCoreMLFeatureValueObservation *> *observations = ARORRunVision(pixelBuffer);
        CVPixelBufferRelease(pixelBuffer);
        if (observations.count == 0)
        {
            return false;
        }

        MLMultiArray *multiArray = nil;
        MLMultiArray *prototypeArray = nil;
        for (VNCoreMLFeatureValueObservation *observation in observations)
        {
            MLMultiArray *candidateArray = observation.featureValue.multiArrayValue;
            if (candidateArray == nil)
            {
                continue;
            }

            if (candidateArray.count >= 84 * 8400 && candidateArray.count % 8400 == 0)
            {
                multiArray = candidateArray;
            }
            else if (candidateArray.count >= 32 * 160 * 160)
            {
                prototypeArray = candidateArray;
            }
        }

        ARORYoloCandidate selected;
        BOOL parsed = ARORParseYoloOutput(
            multiArray,
            prototypeArray,
            imageWidth,
            imageHeight,
            screenWidth,
            screenHeight,
            confidenceThreshold,
            iouThreshold,
            preferredClassId,
            &selected);

        if (!parsed)
        {
            return false;
        }

        *x = selected.x;
        *y = selected.y;
        *width = selected.width;
        *height = selected.height;
        *classId = selected.classId;
        *confidence = selected.confidence;
        *hasMaskBottomCenter = selected.hasMaskBottomCenter ? 1 : 0;
        *maskBottomCenterX = selected.maskBottomCenterX;
        *maskBottomCenterY = selected.maskBottomCenterY;
        *hasMaskCenter = selected.hasMaskCenter ? 1 : 0;
        *maskCenterX = selected.maskCenterX;
        *maskCenterY = selected.maskCenterY;
        return true;
    }
}
