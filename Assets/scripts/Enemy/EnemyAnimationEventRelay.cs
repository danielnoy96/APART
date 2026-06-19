using UnityEngine;

public class EnemyAnimationEventRelay : MonoBehaviour
{
    private EnemyAwareness awareness;
    private EnemyController controller;

    private void Awake()
    {
        awareness = GetComponentInParent<EnemyAwareness>();
        controller = GetComponentInParent<EnemyController>();
    }

    public void PopAnimationFinished()
    {
        if (awareness == null)
            awareness = GetComponentInParent<EnemyAwareness>();

        if (awareness != null)
            awareness.OnPopAnimationFinished();
    }

    public void UnpopAnimationFinished()
    {
        if (awareness == null)
            awareness = GetComponentInParent<EnemyAwareness>();

        if (awareness != null)
            awareness.OnUnpopAnimationFinished();
    }

    public void PatrolPauseStart()
    {
        if (controller == null)
            controller = GetComponentInParent<EnemyController>();

        if (controller != null)
            controller.BeginPatrolAnimationPause();
    }

    public void PatrolPauseEnd()
    {
        if (controller == null)
            controller = GetComponentInParent<EnemyController>();

        if (controller != null)
            controller.EndPatrolAnimationPause();
    }
}
