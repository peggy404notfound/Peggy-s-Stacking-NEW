using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("目标场景名称")]
    public string nextSceneName = "MainScene";  // 在 Inspector 里填你的游戏场景名

    [Header("过渡等待时间(可选)")]
    public float delay = 0.2f;  // 按键到加载的延迟，可设为0

    private bool hasStarted = false; // 防止多次触发

    void Update()
    {
        // 检测任意按键
        if (!hasStarted && Input.anyKeyDown)
        {
            hasStarted = true;
            StartCoroutine(LoadNextScene());
        }
    }

    private System.Collections.IEnumerator LoadNextScene()
    {
        yield return new WaitForSecondsRealtime(delay);

        // 确保使用的是异步加载，不会卡顿
        SceneManager.LoadSceneAsync(nextSceneName);
    }
}