using UnityEngine;

public static class SceneSystemController
{
    private static bool wasCameraRunning = false;
    private static bool wasDetectorRunning = false;
    private static bool wasAlphabetDetectorRunning = false;

    public static void PauseForDialog()
    {
        UniversalCamera camera = Object.FindFirstObjectByType<UniversalCamera>();
        if (camera != null)
        {
            wasCameraRunning = camera.enabled;
            if (wasCameraRunning)
            {
                camera.StopCamera();
                Debug.Log("[SceneSystem] Camera paused for dialog");
            }
        }

        YoloDetector detector = Object.FindFirstObjectByType<YoloDetector>();
        if (detector != null)
        {
            wasDetectorRunning = detector.enabled;
            if (wasDetectorRunning)
            {
                detector.enabled = false;
                Debug.Log("[SceneSystem] YOLO Detector paused for dialog");
            }
        }

        YoloAlphabet alphabetDetector = Object.FindFirstObjectByType<YoloAlphabet>();
        if (alphabetDetector != null)
        {
            wasAlphabetDetectorRunning = alphabetDetector.enabled;
            if (wasAlphabetDetectorRunning)
            {
                alphabetDetector.enabled = false;
                Debug.Log("[SceneSystem] YOLO Alphabet paused for dialog");
            }
        }
    }

    public static void ResumeAfterDialog()
    {
        if (wasCameraRunning)
        {
            UniversalCamera camera = Object.FindFirstObjectByType<UniversalCamera>();
            if (camera != null)
            {
                camera.AllowCameraStart();
                camera.StartCamera();
                Debug.Log("[SceneSystem] Camera resumed after dialog");
            }
            wasCameraRunning = false;
        }

        if (wasDetectorRunning)
        {
            YoloDetector detector = Object.FindFirstObjectByType<YoloDetector>();
            if (detector != null)
            {
                detector.enabled = true;
                Debug.Log("[SceneSystem] YOLO Detector resumed after dialog");
            }
            wasDetectorRunning = false;
        }

        if (wasAlphabetDetectorRunning)
        {
            YoloAlphabet alphabetDetector = Object.FindFirstObjectByType<YoloAlphabet>();
            if (alphabetDetector != null)
            {
                alphabetDetector.enabled = true;
                Debug.Log("[SceneSystem] YOLO Alphabet resumed after dialog");
            }
            wasAlphabetDetectorRunning = false;
        }
    }
}
