using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class NextLevelUI : MonoBehaviour
{
    public TextMeshProUGUI levelEndedText;
    public TextMeshProUGUI finalPointsText;
    public TextMeshProUGUI servedMealsText;

    public void Show(int levelNum, float finalScore, List<string[]> servedMeals)
    {
        if (levelEndedText != null) levelEndedText.text = $"Level {levelNum} ended";
        if (finalPointsText != null) finalPointsText.text = $"Final Points: {finalScore}";
        if (servedMealsText != null) 
        {
            string meals = "Served Meals: [";
            for (int i = 0; i < servedMeals.Count; i++)
            {
                meals += string.Join(", ", servedMeals[i]);
                if (i < servedMeals.Count - 1)
                {
                    meals += ", ";
                }
            }
            meals += "]";
            servedMealsText.text = meals;
        }

        gameObject.SetActive(true);
    }
}
