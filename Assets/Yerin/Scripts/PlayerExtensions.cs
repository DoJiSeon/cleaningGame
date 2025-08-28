using UnityEngine;

public static class PlayerExtensions
{
    public static void TeleportTo(this Player self, Vector3 pos)
    {
        if (!self) return;
        var cc = self.GetComponent<CharacterController>();
        if (cc && cc.enabled)
        {
            cc.enabled = false;
            self.transform.position = pos;
            cc.enabled = true;
        }
        else
        {
            self.transform.position = pos;
        }
    }

    public static void SetInputLocked(this Player self, bool locked)
    {
        if (!self) return;
        // 간단 스토리지(컴파일용): 필요하면 Player에 상태를 들고 가는 게 베스트
        var locker = self.GetComponent<InputLockHolder>() ?? self.gameObject.AddComponent<InputLockHolder>();
        locker.Locked = locked;
    }
}

// 간단 상태 홀더
public class InputLockHolder : MonoBehaviour
{
    public bool Locked;
}
