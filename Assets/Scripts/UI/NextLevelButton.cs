using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class NextLevelButton : MonoBehaviour
{
    private Button button;
    private StudyClient studyClient;

    void Start()
    {
        button = GetComponent<Button>();
        studyClient = FindFirstObjectByType<StudyClient>();

        if (studyClient == null)
        {
            Debug.LogError("StudyClient not found in the scene. The 'Next Level' button will not work.");
            button.interactable = false;
        }
        else
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnButtonClick()
    {
        // Disable the button to prevent multiple clicks
        button.interactable = false;

        // Optionally, change text
        Text buttonText = button.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = "Waiting for others...";
        }

        studyClient.InitiateNextLevel();
    }
}
