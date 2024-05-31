using UnityEngine;
using System.Collections;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI popupText;

    public float fadeTime = 2f; // Duration before the text fades out

    private void Awake()
    {
        if (popupText != null)
            popupText.gameObject.SetActive(false);
    }

    public void ShowMessage(string message)
    {
        popupText.text = message;
        popupText.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeText());
    }

    IEnumerator FadeText()
    {
        popupText.alpha = 1.0f;
        yield return new WaitForSeconds(fadeTime);
        float elapsedTime = 0;
        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime;
            popupText.alpha = Mathf.Clamp01(1.0f - elapsedTime);
            yield return null;
        }
        popupText.gameObject.SetActive(false);
    }
}
