#import <Foundation/Foundation.h>
#import <CoreML/CoreML.h>
#import <Vision/Vision.h>
#import <CoreVideo/CoreVideo.h>
#import <ImageIO/CGImageProperties.h>
#include <float.h>
#include <stdint.h>

struct ARORYoloCandidate
{
    float x;
    float y;
    float width;
    float height;
    int classId;
    float confidence;
};

static VNCoreMLModel *gVisionModel = nil;
static BOOL gLoggedMissingModel = NO;

static float ARORClamp(float value, float minValue, float maxValue)
{
    return fmaxf(minValue, fminf(maxValue, value));
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
    NSURL *modelURL = [bundle URLForResource:@"yolov8n" withExtension:@"mlpackage"];
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
        modelURL = [bundle URLForResource:@"model" withExtension:@"mlmodel" subdirectory:@"yolov8n.mlpackage/Data/com.apple.CoreML"];
    }
    if (modelURL == nil)
    {
        modelURL = [bundle URLForResource:@"model" withExtension:@"mlmodel" subdirectory:@"Data/Raw/yolov8n.mlpackage/Data/com.apple.CoreML"];
    }

    if (modelURL == nil)
    {
        if (!gLoggedMissingModel)
        {
            NSLog(@"[M1 YOLO] yolov8n.mlpackage was not found. Checked app root and Data/Raw StreamingAssets.");
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

    NSLog(@"[M1 YOLO] CoreML YOLO loaded with MLComputeUnitsAll.");
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

static BOOL ARORParseYoloOutput(
    MLMultiArray *array,
    int imageWidth,
    int imageHeight,
    int screenWidth,
    int screenHeight,
    float confidenceThreshold,
    float iouThreshold,
    ARORYoloCandidate *selected)
{
    if (array == nil || array.count < 84 * 8400)
    {
        return NO;
    }

    const int channelCount = 84;
    const int predictionCount = 8400;
    float *values = (float *)array.dataPointer;
    NSMutableArray<NSValue *> *candidates = [NSMutableArray array];

    for (int index = 0; index < predictionCount; index++)
    {
        float bestConfidence = 0.0f;
        int bestClass = -1;
        for (int classIndex = 0; classIndex < channelCount - 4; classIndex++)
        {
            const float confidence = values[(4 + classIndex) * predictionCount + index];
            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestClass = classIndex;
            }
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

        if (candidate.width <= 1.0f || candidate.height <= 1.0f)
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
            if (kept.count >= 20)
            {
                break;
            }
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
        const float x1 = candidate.x - candidate.width * 0.5f;
        const float y1 = candidate.y - candidate.height * 0.5f;
        const float x2 = candidate.x + candidate.width * 0.5f;
        const float y2 = candidate.y + candidate.height * 0.5f;
        if (centerX >= x1 && centerX <= x2 && centerY >= y1 && centerY <= y2 && candidate.confidence > best.confidence)
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
        float *x,
        float *y,
        float *width,
        float *height,
        int *classId,
        float *confidence)
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

        MLMultiArray *multiArray = observations.firstObject.featureValue.multiArrayValue;
        ARORYoloCandidate selected;
        BOOL parsed = ARORParseYoloOutput(
            multiArray,
            imageWidth,
            imageHeight,
            screenWidth,
            screenHeight,
            confidenceThreshold,
            iouThreshold,
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
        return true;
    }
}
