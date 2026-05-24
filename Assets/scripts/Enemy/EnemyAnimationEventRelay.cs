using UnityEngine;

public class EnemyAnimationEventRelay : MonoBehaviour
{
    private EnemyAwareness awareness;

    private void Awake()
    {
        awareness = GetComponentInParent<EnemyAwareness>();
    }

    public void PopAnimationFinished()
    {
        if (awareness == null)
            awareness = GetComponentInParent<EnemyAwareness>();

        if (awareness != null)
            awareness.OnPopAnimationFinished();
    }
}
