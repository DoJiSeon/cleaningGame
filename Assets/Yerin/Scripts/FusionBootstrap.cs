using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionBootstrap : MonoBehaviour
{
    [Header("세션 설정")]
    [SerializeField] private string sessionName = "TEST-SESSION";
    [SerializeField] private GameMode gameMode = GameMode.AutoHostOrClient;

    [Header("씬 전환 옵션")]
    [Tooltip("true면 현재 열려있는 씬(buildIndex)로 세션 시작, false면 씬 전환 없이 그대로 시작")]
    [SerializeField] private bool loadCurrentSceneOnStart = true;

    [Header("러너 프리팹(선택)")]
    [Tooltip("비워두면 코드에서 새 GameObject에 NetworkRunner를 직접 추가합니다.")]
    [SerializeField] private NetworkRunner runnerPrefab;

    private NetworkRunner _runner;

    private async void Start()
    {
        Application.runInBackground = true;

        // Runner 생성 또는 재사용
        _runner = FindObjectOfType<NetworkRunner>();
        if (_runner == null)
        {
            _runner = runnerPrefab ? Instantiate(runnerPrefab)
                                   : new GameObject("NetworkRunner").AddComponent<NetworkRunner>();
        }

        // 입력 동기화 필요 시
        _runner.ProvideInput = true;

        // 기본 씬 매니저 부착
        var sceneMgr = _runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneMgr == null) sceneMgr = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        // Fusion 2: Scene 은 NetworkSceneInfo? 타입
        NetworkSceneInfo? sceneInfo = null;
        if (loadCurrentSceneOnStart)
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;

            // 핵심: SceneRef.FromIndex → NetworkSceneInfo 로 암시 변환 가능
            sceneInfo = SceneRef.FromIndex(buildIndex);

            // (대안) 명시 구성:
            // var nsi = default(NetworkSceneInfo);
            // nsi.AddSceneRef(SceneRef.FromIndex(buildIndex));
            // sceneInfo = nsi;
        }

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            SceneManager = sceneMgr,
            Scene = sceneInfo // null이면 씬 전환 없이 시작
        });

        if (!result.Ok)
        {
            Debug.LogError($"[FusionBootstrap] StartGame failed: {result.ShutdownReason}");
        }
        else
        {
            Debug.Log($"[FusionBootstrap] StartGame OK: {gameMode} / {sessionName}");
        }
    }
}
