using UnityEngine;

public interface IRingInteractable
{
    // 修改前：void OnRingHit(Vector3 ringCenter);
    // 修改后：传入圆环脚本本身，这样物体就能读取圆环的位置，也能调用圆环的方法
    void OnRingHit(ExpandingRing ring);
}
