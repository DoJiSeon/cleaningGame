using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : NetworkBehaviour
{
    public string message;
    public UnityEvent onInteraction;   // FX/사운드/애니만 연결 (절대 Interact() 연결 금지)
    public Renderer MyRenderer;

    Outline outline;

    void Start()
    {
        outline = GetComponent<Outline>();
        DisableOutline();
    }

    // 로컬에서 눌림 → 서버에 처리 요청
    public void Interact()
    {
        var no = GetComponent<NetworkObject>();
        if (no != null)
            RPC_RequestInteract(no.Id);   // ★ 이벤트 호출 없음
    }

    // 클라/서버 누구나 호출 가능, 처리자는 StateAuthority
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestInteract(NetworkId targetId, RpcInfo _ = default)
    {
        // 1) 서버에서 대상 찾기
        var obj = Runner.FindObject(targetId);
        if (obj == null) return;

        //// 2) 요청 보낸 플레이어 가져오기
        //var playerObj = Runner.GetPlayerObject(_.Source);
        //if (playerObj == null) return;

        // 3) 플레이어의 EquipManagerNet 확인
        //var equip = playerObj.GetComponent<EquipManagerNet>();
        //if (equip == null) return;

        //if (equip.Equipped != EquipmentId.Hand)
        //{
        //    // Hand가 아니면 무시 (원하면 로그/UI 피드백)
        //    Debug.Log("Hand 아님");
        //    return;
            
        //}


        // 모든 클라에서 연출(이벤트) 먼저
        RPC_PlayFX(targetId);
        // 모든 클라에서 페이드 시작
        RPC_PlayFade(targetId, 0.02f);
        // 페이드 시간 지난 후 despawn
        StartCoroutine(DespawnAfterDelay(targetId, 1.0f));
    }

    private IEnumerator DespawnAfterDelay(NetworkId targetId, float delay)
    {
        yield return new WaitForSeconds(delay);
        var obj = Runner.FindObject(targetId);
        if (obj != null) Runner.Despawn(obj);
    }

    // 연출(이벤트)을 네트워크로 브로드캐스트
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFX(NetworkId targetId)
    {
        var obj = Runner.FindObject(targetId);
        if (!obj) return;

        var it = obj.GetComponent<Interactable>();
        // ★ 여기서만 onInteraction 호출 (절대 Interact() 호출 금지)
        it?.onInteraction?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFade(NetworkId targetId, float step)
    {
        var obj = Runner.FindObject(targetId);
        if (!obj) return;
        var it = obj.GetComponent<Interactable>();
        if (it) it.StartCoroutine(it.FadeOut(step));
    }

    private IEnumerator FadeOut(float step)
    {
        var mat = MyRenderer ? MyRenderer.material : null;
        if (!mat) yield break;

        float a = mat.color.a;
        while (a > 0f)
        {
            a -= step;
            var c = mat.color; c.a = Mathf.Max(0, a);
            mat.color = c;
            yield return new WaitForSeconds(step);
        }
    }

    public void DisableOutline() { if (outline) outline.enabled = false; }
    public void EnableOutline() { if (outline) outline.enabled = true; }
}


//public class Interactable : MonoBehaviour
//{
//    // Start is called before the first frame update
//    Outline outline;
//    public string message;
//    private EquipmentState currentState = EquipmentState.None;

//    public UnityEvent onIntercation;

//    public Renderer MyRenderer;
//    void Start()
//    {
//        outline = GetComponent<Outline>();
//        DisableOutline();
//    }

//    public void Interact()
//    {
//        onIntercation.Invoke();
//    }

//    public void DisableOutline()
//    {
//        if (outline != null)
//            outline.enabled = false;
//    }

//    public void EnableOutline()
//    {
//        if(outline != null)
//            outline.enabled = true;
//    }

//    public void Destroy()
//    {
//        Destroy(gameObject);
//    }

//    public void FadeDestroy()
//    {
//        StartCoroutine(FadeOut());

//    }
//    public void TryUseSponge(GameObject interactor)
//    {
//        if (currentState == EquipmentState.Sponge)
//        {
//            SpongeEquipment playerSponge = interactor.GetComponentInChildren<SpongeEquipment>();

//            if (playerSponge == null)
//            {
//                Debug.LogWarning("스펀지가 없습니다.");
//                return;
//            }

//            if (playerSponge.isDirty)
//            {
//                Debug.Log("청소 불가(스펀지 더러움)");
//                return;
//            }

//            StartCoroutine(FadeOut());
//        }


//    }

//    public void Wash(GameObject interactor)
//    {
//        SpongeEquipment playerSponge = interactor.GetComponentInChildren<SpongeEquipment>();
//        if (playerSponge != null && playerSponge.isDirty)
//        {
//            playerSponge.WashSponge();
//            Debug.Log("플레이어의 스펀지를 양동이에서 세척했습니다.");
//        }
//        else
//        {
//            Debug.Log("세척할 스펀지가 없거나 깨끗합니다.");
//        }
//    }

//    IEnumerator FadeOut()
//    {
//        float f = 1;
//        while (f > 0)
//        {
//            f -= 0.02f;
//            Color ColorAlhpa = MyRenderer.material.color;
//            ColorAlhpa.a = f;
//            MyRenderer.material.color = ColorAlhpa;
//            yield return new WaitForSeconds(0.02f);
//        }
//        Destroy(gameObject);
//    }

//    void Update()
//    {

//    }
//}
