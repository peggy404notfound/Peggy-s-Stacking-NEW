using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButton : MonoBehaviour
{
    [SerializeField] string gameSceneName = "Game";

    // 绑定到 Button 的 OnClick
    public void StartGame()
    {
        // 防止你在游戏里暂停过，回到1
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}