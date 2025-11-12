using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonPopupManager : MonoBehaviour
{
    public GameObject[] popups;
    private GameObject currentPopup;

    public void ShowPopup(int index)
    {
        HideAll(); // 기존 팝업들 다 끄고
        if (index >= 0 && index < popups.Length)
        {
            currentPopup = popups[index];
            currentPopup.SetActive(true);
        }
    }

    public void HideAll()
    {
        foreach (GameObject popup in popups)
            popup.SetActive(false);
        currentPopup = null;
    }

    public void HideCurrent()
    {
        if (currentPopup != null)
        {
            currentPopup.SetActive(false);
            currentPopup = null;
        }
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료!");

#if UNITY_EDITOR
        // Unity 에디터에서 실행중일 때
        UnityEditor.EditorApplication.isPlaying = false;
#else
            // 빌드된 게임에서
            Application.Quit();
#endif
    }
}
