using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButton : MonoBehaviour
{
    [SerializeField] string gameSceneName = "Game";

    // �󶨵� Button �� OnClick
    public void StartGame()
    {
        // ��ֹ������Ϸ����ͣ�����ص�1
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}