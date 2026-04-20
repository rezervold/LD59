using UnityEngine;

[DisallowMultipleComponent]
public class HordeTargetTracker : MonoBehaviour
{
    public static Transform Target { get; private set; }

    private void OnEnable()
    {
        Target = transform;
    }

    private void OnDisable()
    {
        if (Target == transform)
            Target = null;
    }
}
