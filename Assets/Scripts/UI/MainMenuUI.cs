using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    
    [Header("Seed Settings")]
    [SerializeField] private TMPro.TMP_InputField seedInput;
    [SerializeField] private Toggle useSeedToggle;

    private void Awake()
    {
        // Setup Seed UI Logic
        if (useSeedToggle != null && seedInput != null)
        {
            // Initial State
            seedInput.gameObject.SetActive(useSeedToggle.isOn);

            // Listener
            useSeedToggle.onValueChanged.AddListener((isOn) =>
            {
                seedInput.gameObject.SetActive(isOn);
            });
        }

        playButton.onClick.AddListener(() =>
        {
            // Handle Seed Logic
            if (useSeedToggle != null && useSeedToggle.isOn && seedInput != null && !string.IsNullOrEmpty(seedInput.text))
            {
                if (int.TryParse(seedInput.text, out int parsedSeed))
                {
                    GameSettings.FixedSeed = parsedSeed;
                    Debug.Log($"[MainMenu] Fixed Seed Set: {parsedSeed}");
                }
                else
                {
                   // Invalid text (though TMP should limit to int if configured)
                   GameSettings.FixedSeed = null;
                   Debug.Log("[MainMenu] Invalid Seed Text. Using Random.");
                }
            }
            else
            {
                // Toggle off or empty text -> Random
                GameSettings.FixedSeed = null;
                Debug.Log("[MainMenu] Using Random Seed.");
            }

            Loader.Load(Loader.Scene.GameScene);
        });

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }

}
