using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro; // TextMeshPro 사용시
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    [Header("=== GAMEPLAY SETTINGS ===")]
    public Slider mouseSensitivitySlider;
    public TextMeshProUGUI mouseSensitivityText;

    public Slider fovSlider;
    public TextMeshProUGUI fovText;

    public Toggle cameraShakeToggle;
    public Toggle subtitlesToggle;
    public Toggle autoSaveToggle;

    public TMP_Dropdown difficultyDropdown;

    [Header("=== GRAPHICS SETTINGS ===")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown fullscreenDropdown;
    public TMP_Dropdown antiAliasingDropdown;
    public TMP_Dropdown shadowQualityDropdown;

    public Toggle vsyncToggle;

    public Slider frameRateLimitSlider;
    public TextMeshProUGUI frameRateLimitText;

    [Header("=== AUDIO SETTINGS ===")]
    public AudioMixer audioMixer;

    public Slider masterVolumeSlider;
    public TextMeshProUGUI masterVolumeText;

    public Slider musicVolumeSlider;
    public TextMeshProUGUI musicVolumeText;

    public Slider sfxVolumeSlider;
    public TextMeshProUGUI sfxVolumeText;

    public Slider voiceVolumeSlider;
    public TextMeshProUGUI voiceVolumeText;

    public Toggle muteAllToggle;

    [Header("=== CONTROLS SETTINGS ===")]
    public Toggle invertYToggle;

    public Slider controllerVibrationSlider;
    public TextMeshProUGUI controllerVibrationText;

    private Resolution[] resolutions;
    private bool isInitialized = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {

        SetupAllDropdowns();
        LoadAllSettings();
        AddAllListeners();
        isInitialized = true;
    }

    // ========================================
    // 모든 UI 리스너 등록
    // ========================================
    void AddAllListeners()
    {
        // GAMEPLAY
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        fovSlider.onValueChanged.AddListener(OnFOVChanged);
        cameraShakeToggle.onValueChanged.AddListener(OnCameraShakeChanged);
        subtitlesToggle.onValueChanged.AddListener(OnSubtitlesChanged);
        autoSaveToggle.onValueChanged.AddListener(OnAutoSaveChanged);
        difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);

        // GRAPHICS
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        fullscreenDropdown.onValueChanged.AddListener(OnFullscreenChanged);
        antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
        shadowQualityDropdown.onValueChanged.AddListener(OnShadowQualityChanged);
        vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        frameRateLimitSlider.onValueChanged.AddListener(OnFrameRateLimitChanged);

        // AUDIO
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
        muteAllToggle.onValueChanged.AddListener(OnMuteAllChanged);

        // CONTROLS
        invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        controllerVibrationSlider.onValueChanged.AddListener(OnControllerVibrationChanged);
    }

    // ========================================
    // GAMEPLAY 설정
    // ========================================
    void OnMouseSensitivityChanged(float value)
    {
        mouseSensitivityText.text = value.ToString("F1");
        PlayerPrefs.SetFloat("MouseSensitivity", value);

        // 실제 플레이어 컨트롤러에 적용
        // if (PlayerController.Instance != null)
        //     PlayerController.Instance.mouseSensitivity = value;
    }

    void OnFOVChanged(float value)
    {
        fovText.text = Mathf.RoundToInt(value).ToString();
        PlayerPrefs.SetFloat("FOV", value);

        // 메인 카메라 FOV 적용
        if (Camera.main != null)
            Camera.main.fieldOfView = value;
    }

    void OnCameraShakeChanged(bool value)
    {
        PlayerPrefs.SetInt("CameraShake", value ? 1 : 0);
    }

    void OnSubtitlesChanged(bool value)
    {
        PlayerPrefs.SetInt("Subtitles", value ? 1 : 0);
    }

    void OnAutoSaveChanged(bool value)
    {
        PlayerPrefs.SetInt("AutoSave", value ? 1 : 0);
    }

    void OnDifficultyChanged(int index)
    {
        PlayerPrefs.SetInt("Difficulty", index);
        // 0: Easy, 1: Normal, 2: Hard
    }

    // ========================================
    // GRAPHICS 설정
    // ========================================
    void SetupAllDropdowns()
    {
        // Resolution 드롭다운 설정
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> resolutionOptions = new List<string>();
        HashSet<string> uniqueResolutions = new HashSet<string>(); // 중복 제거용
        int currentResolutionIndex = 0;
        int uniqueIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            // 주사율 무시하고 해상도만으로 중복 체크
            string option = resolutions[i].width + " x " + resolutions[i].height;

            if (!uniqueResolutions.Contains(option))
            {
                uniqueResolutions.Add(option);
                resolutionOptions.Add(option);

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = uniqueIndex;
                }
                uniqueIndex++;
            }
        }

        resolutionDropdown.AddOptions(resolutionOptions);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // Quality 드롭다운
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));

        // Fullscreen 드롭다운
        fullscreenDropdown.ClearOptions();
        fullscreenDropdown.AddOptions(new List<string> { "Fullscreen", "Windowed", "Borderless" });

        // Anti-Aliasing 드롭다운
        antiAliasingDropdown.ClearOptions();
        antiAliasingDropdown.AddOptions(new List<string> { "Off", "2x", "4x", "8x" });

        // Shadow Quality 드롭다운
        shadowQualityDropdown.ClearOptions();
        shadowQualityDropdown.AddOptions(new List<string> { "Off", "Low", "Medium", "High" });
    }

    void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutions.Length) return;

        Resolution resolution = resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt("ResolutionIndex", index);
    }

    void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt("QualityLevel", index);
    }

    void OnFullscreenChanged(int index)
    {
        switch (index)
        {
            case 0: // Fullscreen
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case 1: // Windowed
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case 2: // Borderless
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
        }
        PlayerPrefs.SetInt("FullscreenMode", index);
    }

    void OnAntiAliasingChanged(int index)
    {
        int aaValue = 0;
        switch (index)
        {
            case 0: aaValue = 0; break;  // Off
            case 1: aaValue = 2; break;  // 2x
            case 2: aaValue = 4; break;  // 4x
            case 3: aaValue = 8; break;  // 8x
        }
        QualitySettings.antiAliasing = aaValue;
        PlayerPrefs.SetInt("AntiAliasing", index);
    }

    void OnShadowQualityChanged(int index)
    {
        QualitySettings.shadows = (ShadowQuality)index;
        PlayerPrefs.SetInt("ShadowQuality", index);
    }

    void OnVSyncChanged(bool value)
    {
        QualitySettings.vSyncCount = value ? 1 : 0;
        PlayerPrefs.SetInt("VSync", value ? 1 : 0);
    }

    void OnFrameRateLimitChanged(float value)
    {
        int frameRate = Mathf.RoundToInt(value);
        frameRateLimitText.text = frameRate == 144 ? "Unlimited" : frameRate.ToString();

        Application.targetFrameRate = frameRate == 144 ? -1 : frameRate;
        PlayerPrefs.SetInt("FrameRateLimit", frameRate);
    }

    // ========================================
    // AUDIO 설정
    // ========================================
    void OnMasterVolumeChanged(float value)
    {
        masterVolumeText.text = Mathf.RoundToInt(value * 100) + "%";

        if (muteAllToggle.isOn)
        {
            audioMixer.SetFloat("MasterVolume", -80f);
        }
        else
        {
            float db = value > 0 ? Mathf.Log10(value) * 20 : -80f;
            audioMixer.SetFloat("MasterVolume", db);
        }

        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    void OnMusicVolumeChanged(float value)
    {
        musicVolumeText.text = Mathf.RoundToInt(value * 100) + "%";

        float db = value > 0 ? Mathf.Log10(value) * 20 : -80f;
        audioMixer.SetFloat("MusicVolume", db);

        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    void OnSFXVolumeChanged(float value)
    {
        sfxVolumeText.text = Mathf.RoundToInt(value * 100) + "%";

        float db = value > 0 ? Mathf.Log10(value) * 20 : -80f;
        audioMixer.SetFloat("SFXVolume", db);

        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    void OnVoiceVolumeChanged(float value)
    {
        voiceVolumeText.text = Mathf.RoundToInt(value * 100) + "%";

        float db = value > 0 ? Mathf.Log10(value) * 20 : -80f;
        audioMixer.SetFloat("VoiceVolume", db);

        PlayerPrefs.SetFloat("VoiceVolume", value);
    }

    void OnMuteAllChanged(bool value)
    {
        if (value)
        {
            audioMixer.SetFloat("MasterVolume", -80f);
        }
        else
        {
            OnMasterVolumeChanged(masterVolumeSlider.value);
        }

        PlayerPrefs.SetInt("MuteAll", value ? 1 : 0);
    }

    // ========================================
    // CONTROLS 설정
    // ========================================
    void OnInvertYChanged(bool value)
    {
        PlayerPrefs.SetInt("InvertY", value ? 1 : 0);

        // if (PlayerController.Instance != null)
        //     PlayerController.Instance.invertY = value;
    }

    void OnControllerVibrationChanged(float value)
    {
        controllerVibrationText.text = Mathf.RoundToInt(value * 100) + "%";
        PlayerPrefs.SetFloat("ControllerVibration", value);
    }

    // ========================================
    // 설정 불러오기
    // ========================================
    void LoadAllSettings()
    {
        // GAMEPLAY
        mouseSensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 5f);
        fovSlider.value = PlayerPrefs.GetFloat("FOV", 90f);
        cameraShakeToggle.isOn = PlayerPrefs.GetInt("CameraShake", 1) == 1;
        subtitlesToggle.isOn = PlayerPrefs.GetInt("Subtitles", 0) == 1;
        autoSaveToggle.isOn = PlayerPrefs.GetInt("AutoSave", 1) == 1;
        difficultyDropdown.value = PlayerPrefs.GetInt("Difficulty", 1);

        // GRAPHICS
        resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionIndex", resolutions.Length - 1);
        qualityDropdown.value = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
        fullscreenDropdown.value = PlayerPrefs.GetInt("FullscreenMode", 0);
        antiAliasingDropdown.value = PlayerPrefs.GetInt("AntiAliasing", 2);
        shadowQualityDropdown.value = PlayerPrefs.GetInt("ShadowQuality", 2);
        vsyncToggle.isOn = PlayerPrefs.GetInt("VSync", 1) == 1;
        frameRateLimitSlider.value = PlayerPrefs.GetInt("FrameRateLimit", 60);

        // AUDIO
        masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
        voiceVolumeSlider.value = PlayerPrefs.GetFloat("VoiceVolume", 1f);
        muteAllToggle.isOn = PlayerPrefs.GetInt("MuteAll", 0) == 1;

        // CONTROLS
        invertYToggle.isOn = PlayerPrefs.GetInt("InvertY", 0) == 1;
        controllerVibrationSlider.value = PlayerPrefs.GetFloat("ControllerVibration", 0.5f);
    }

    // ========================================
    // 공개 함수들
    // ========================================
    public void ResetToDefault()
    {
        PlayerPrefs.DeleteAll();
        LoadAllSettings();
        Debug.Log("Settings reset to default!");
    }

    public void SaveSettings()
    {
        PlayerPrefs.Save();
        Debug.Log("Settings saved!");
    }
}