using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : NetworkBehaviour
{
    public string message;
    public UnityEvent onInteraction;   // FX/����/�ִϸ� ���� (���� Interact() ���� ����)
    public Renderer MyRenderer;

    // RPC 연출 중 UnityEvent가 다시 Interact()를 호출하여 재귀되는 것을 막기 위한 가드
    private bool _suppressInteractWhileFx;

    Outline outline;

    void Start()
    {
        outline = GetComponent<Outline>();
        DisableOutline();
    }

    // ���ÿ��� ���� �� ������ ó�� ��û
    public void Interact()
    {
        if (_suppressInteractWhileFx) return;
        var no = GetComponent<NetworkObject>();
        if (no == null) no = GetComponentInParent<NetworkObject>();
        if (no != null)
            RPC_RequestInteract(no.Id);   // �� �̺�Ʈ ȣ�� ����
    }

    // Ŭ��/���� ������ ȣ�� ����, ó���ڴ� StateAuthority
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestInteract(NetworkId targetId, RpcInfo _ = default)
    {
        // 1) �������� ��� ã��
        var obj = Runner.FindObject(targetId);
        if (obj == null)
        {
            Debug.LogWarning($"[Interactable] RPC_RequestInteract: target not found {targetId}");
            return;
        }

        //// 2) ��û ���� �÷��̾� ��������
        //var playerObj = Runner.GetPlayerObject(_.Source);
        //if (playerObj == null) return;

        // 3) �÷��̾��� EquipManagerNet Ȯ��
        //var equip = playerObj.GetComponent<EquipManagerNet>();
        //if (equip == null) return;

        //if (equip.Equipped != EquipmentId.Hand)
        //{
        //    // Hand�� �ƴϸ� ���� (���ϸ� �α�/UI �ǵ��)
        //    Debug.Log("Hand �ƴ�");
        //    return;
            
        //}


        // ��� Ŭ�󿡼� ����(�̺�Ʈ) ����
        RPC_PlayFX(targetId);
        // ��� Ŭ�󿡼� ���̵� ����
        RPC_PlayFade(targetId, 0.02f);
        // ���̵� �ð� ���� �� despawn
        // 재상호작용 방지: 즉시 콜라이더 비활성화
        try
        {
            foreach (var col in obj.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }
        catch { }

        StartCoroutine(DespawnAfterDelay(targetId, 0.5f));
    }

    private IEnumerator DespawnAfterDelay(NetworkId targetId, float delay)
    {
        yield return new WaitForSeconds(delay);
        var obj = Runner.FindObject(targetId);
        if (obj == null)
        {
            Debug.LogWarning($"[Interactable] DespawnAfterDelay: target already null {targetId}");
            yield break;
        }
        if (Runner)
        {
            Debug.Log($"[Interactable] Despawning {obj.name} ({targetId})");
            Runner.Despawn(obj);
        }
        else
        {
            Debug.LogWarning("[Interactable] DespawnAfterDelay: Runner missing");
        }
    }

    // ����(�̺�Ʈ)�� ��Ʈ��ũ�� ��ε�ĳ��Ʈ
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFX(NetworkId targetId)
    {
        var obj = Runner.FindObject(targetId);
        if (!obj) return;

        var it = obj.GetComponent<Interactable>();
        // �� ���⼭�� onInteraction ȣ��
        // 주의: UnityEvent가 Interact()를 다시 호출하도록 연결되어 있을 수 있으므로 재귀 방지 가드
        if (it != null)
        {
            it._suppressInteractWhileFx = true;
            try
            {
                it.onInteraction?.Invoke();
            }
            finally
            {
                it._suppressInteractWhileFx = false;
            }
        }
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
//                Debug.LogWarning("�������� �����ϴ�.");
//                return;
//            }

//            if (playerSponge.isDirty)
//            {
//                Debug.Log("û�� �Ұ�(������ ������)");
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
//            Debug.Log("�÷��̾��� �������� �絿�̿��� ��ô�߽��ϴ�.");
//        }
//        else
//        {
//            Debug.Log("��ô�� �������� ���ų� �����մϴ�.");
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
