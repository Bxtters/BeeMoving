using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Saved choices, phone pairing steps, and the USB setup.
    /// Added so those instructions are not inside the camera or pose code.
    /// The picture is still only shown by the camera stage. Nothing here is uploaded.
    /// </summary>
    public sealed partial class Camera_MoCap
    {
        const string PairingGuide =
            "IPHONE ON A MAC\n\n" +
            "1. On the iPhone, open Settings, then General, then AirPlay & Continuity.\n" +
            "2. Turn Continuity Camera on.\n" +
            "3. Sign the iPhone and the Mac into the same Apple ID.\n" +
            "4. Turn Wi-Fi and Bluetooth on for both. Unlock the iPhone, leave it near the Mac, and do not let it sleep.\n" +
            "5. Press Refresh. Choose the iPhone in the camera row.\n\n" +
            "IPHONE ON WINDOWS\n\n" +
            "Continuity Camera is a Mac feature. Windows does not see an iPhone until a webcam app publishes it.\n\n" +
            "1. On the PC, install Iriun Webcam from iriun.com and open it. If Windows asks to install a camera driver, allow it.\n" +
            "2. On the iPhone, install Iriun Webcam from the App Store.\n" +
            "3. Join the same Wi-Fi on the phone and the PC. Or plug the iPhone in with a data cable and tap Trust.\n" +
            "4. Open Iriun on the iPhone and allow the camera.\n" +
            "5. Leave Iriun open on Windows so the phone is registered as a webcam.\n" +
            "6. Press Refresh here and select the camera named Iriun.\n\n" +
            "ANDROID\n\n" +
            "1. Install Iriun Webcam or DroidCam on the phone and on this computer.\n" +
            "2. Open the app on both. Allow the camera on the phone.\n" +
            "3. Use the same Wi-Fi, or USB when the app offers it.\n" +
            "4. Press Refresh here and select that camera.\n\n" +
            "A plugged-in iPhone is a different setup. Press Back, then USB cable.\n\n" +
            "This app only uses a webcam the computer already lists. It does not install a driver. The picture stays in this window and is not saved.";
        const string CableGuide =
            "Use the charging cable that came with the iPhone, or another data cable. A charge-only cable powers the phone and never becomes a camera.\n\n" +
            "MAC\n\n" +
            "1. Plug the iPhone into the Mac.\n" +
            "2. Unlock the iPhone. If it asks to Trust This Computer, tap Trust and enter the passcode.\n" +
            "3. Leave the phone unlocked. Continuity Camera uses the cable while it is plugged in.\n" +
            "4. Press Check cable. This page selects the iPhone when the Mac lists it.\n\n" +
            "WINDOWS\n\n" +
            "Windows does not speak iPhone USB until Apple's phone service is installed. Until then the cable only charges the phone.\n\n" +
            "1. Press Apple driver and install Apple Devices. Open it once. iTunes from Apple does the same job if you already have it.\n" +
            "2. Plug the iPhone into a USB port on the PC. Unlock it, tap Trust, and enter the passcode.\n" +
            "3. If the PC still does not see the phone, press Windows+R, type services.msc, and start Apple Mobile Device Service. Set its startup type to Automatic.\n" +
            "4. Press Iriun and install Iriun Webcam on the PC. Open the PC app first and leave it open.\n" +
            "5. Install Iriun Webcam on the iPhone from the App Store. Open it, allow the camera, and leave the cable plugged in. Iriun uses the cable when it is connected.\n" +
            "6. Press Check cable. This page selects the camera named Iriun when Windows lists it.\n\n" +
            "The picture stays in this window. It is not saved.";
        const string AppleDriverUrl = "https://apps.microsoft.com/detail/9np83lwlpz9k";
        const string IriunUrl = "https://iriun.com/";

        /// <summary>
        /// Reads display choices saved on this machine. Nothing here is uploaded.
        /// </summary>
        void LoadSettings()
        {
            mirror = PlayerPrefs.GetInt(PrefMirror, 1) == 1;
            extraRotation = PlayerPrefs.GetInt(PrefRotation, 0);
            if (extraRotation != 0 && extraRotation != 90 && extraRotation != 180 && extraRotation != 270) extraRotation = 0;
            requestWidth = PlayerPrefs.GetInt(PrefWidth, 640);
            requestHeight = PlayerPrefs.GetInt(PrefHeight, 480);
            if (!KnownResolution(requestWidth, requestHeight))
            {
                requestWidth = 0;
                requestHeight = 0;
            }

            requestFps = PlayerPrefs.GetInt(PrefFps, 30);
            if (requestFps != 15 && requestFps != 30 && requestFps != 60) requestFps = 30;
            peopleLimit = Mathf.Clamp(PlayerPrefs.GetInt(PrefPeople, 1), 1, MaxPeople);
            jointThreshold = PlayerPrefs.GetFloat(PrefThreshold, 0.2f);
            if (jointThreshold < 0.1f || jointThreshold > 0.6f) jointThreshold = 0.2f;
            savedCameraName = PlayerPrefs.GetString(PrefCamera, "");
        }


        /// <summary>
        /// Stores the current display choices in Unity PlayerPrefs on this machine.
        /// </summary>
        void SaveSettings()
        {
            PlayerPrefs.SetInt(PrefMirror, mirror ? 1 : 0);
            PlayerPrefs.SetInt(PrefRotation, extraRotation);
            PlayerPrefs.SetInt(PrefWidth, requestWidth);
            PlayerPrefs.SetInt(PrefHeight, requestHeight);
            PlayerPrefs.SetInt(PrefFps, requestFps);
            PlayerPrefs.SetInt(PrefPeople, peopleLimit);
            PlayerPrefs.SetFloat(PrefThreshold, jointThreshold);
            PlayerPrefs.SetString(PrefCamera, savedCameraName ?? "");
            PlayerPrefs.Save();
        }


        /// <summary>
        /// True for the three resolutions the settings menu offers.
        /// </summary>
        static bool KnownResolution(int width, int height)
        {
            return (width == 0 && height == 0) || (width == 640 && height == 480) || (width == 1280 && height == 720) || (width == 1920 && height == 1080);
        }


        /// <summary>
        /// Selects a phone webcam and writes the connected line. Returns false when none is listed.
        /// </summary>
        bool TryConnectPhone(WebCamDevice[] devices, string connectedLine)
        {
            int phone = FindPhone(devices);
            if (phone < 0) return false;
            if (devices != null) cachedDevices = devices;
            waitingForPhone = false;
            waitingForCable = false;
            cameraHeldOff = false;
            if (pairingStatus != null) pairingStatus.text = connectedLine;
            if (cableStatus != null) cableStatus.text = connectedLine;
            if (!ModelReady())
            {
                pendingCamera = phone;
                if (!resuming) StartCoroutine(Resume());
                return true;
            }

            OpenCamera(phone);
            SetStatus(connectedLine);
            return true;
        }


        /// <summary>
        /// Opens the Apple Devices install page. Windows needs it before an iPhone USB cable is more than power.
        /// </summary>
        void OpenAppleDriver()
        {
            Application.OpenURL(AppleDriverUrl);
        }


        /// <summary>
        /// Opens the Iriun Webcam site. The Windows app is what publishes the plugged-in iPhone as a webcam.
        /// </summary>
        void OpenIriun()
        {
            Application.OpenURL(IriunUrl);
        }


        /// <summary>
        /// Finds a device whose name looks like a phone or a phone webcam app.
        /// </summary>
        static int FindPhone(WebCamDevice[] devices)
        {
            if (devices == null) return -1;
            for (int i = 0; i < devices.Length; i++)
            {
                if (LooksLikePhone(devices[i])) return i;
            }

            return -1;
        }


        /// <summary>
        /// True when a device name matches a phone, Continuity Camera, or a common phone webcam app.
        /// </summary>
        static bool LooksLikePhone(WebCamDevice device)
        {
            string name = device.name == null ? "" : device.name.ToLowerInvariant();
            if (name.IndexOf("iphone") >= 0 || name.IndexOf("ipad") >= 0 || name.IndexOf("continuity") >= 0) return true;
            if (name.IndexOf("android") >= 0 || name.IndexOf("droidcam") >= 0 || name.IndexOf("iriun") >= 0) return true;
            if (name.IndexOf("epoccam") >= 0 || name.IndexOf("camo") >= 0 || name.IndexOf("galaxy") >= 0) return true;
            if (name.IndexOf("pixel") >= 0 || name.IndexOf("phone") >= 0) return true;
            return false;
        }

    }
}
