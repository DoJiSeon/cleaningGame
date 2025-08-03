using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingSceneManager : MonoBehaviour
{
    public static string nextScene;

    [Header("UI 참조")]
    [SerializeField] private Slider progressSlider; // 0~1 범위
    [SerializeField] private TMP_Text progressText; // "XX %"

    [Header("페이크 로딩 시간 (초)")]
    [SerializeField] private float fakeTotalLoadTime = 3f;

    private void Start()
    {
        StartCoroutine(LoadSceneCoroutine());
    }

    /// <summary>
    /// 외부에서 호출 → Loading 씬으로 넘어가도록
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        nextScene = sceneName;
        SceneManager.LoadScene("Loading");
    }

    private IEnumerator LoadSceneCoroutine()
    {
        yield return null; // 한 프레임 대기

        // 실제 비동기 로드 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        float startTime = Time.unscaledTime;
        bool loadDone = false;

        while (true)
        {
            yield return null;

            // 1) 페이크 타이머 기반 슬라이더
            float fakeElapsed = Time.unscaledTime - startTime;
            float fakeProgress = Mathf.Clamp01(fakeElapsed / fakeTotalLoadTime);
            progressSlider.value = fakeProgress;
            progressText.text = $"{Mathf.RoundToInt(fakeProgress * 100f)} %";

            // 2) 실제 로딩 상태 체크 (op.progress는 0~0.9 까지만 올라가니 0.9 이상이면 준비 완료)
            if (!loadDone && op.progress >= 0.9f)
            {
                loadDone = true;
            }

            // 3) 둘 다 끝났으면 바로 씬 전환
            if (fakeProgress >= 1f && loadDone)
            {
                op.allowSceneActivation = true;
                yield break;
            }
        }
    }
}
