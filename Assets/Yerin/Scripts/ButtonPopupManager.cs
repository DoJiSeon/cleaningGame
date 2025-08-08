using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonPopupManager : MonoBehaviour
{
    public GameObject[] popups;
    private GameObject currentPopup;

    public void ShowPopup(int index)
    {
        HideAll(); // ±âÁ¸ ÆË¾÷µé ´Ù ²ô°í
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
}
