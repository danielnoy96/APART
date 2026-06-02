using UnityEngine;

public class PlayerGroundShadow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float maxGroundDistance = 10f;
    [SerializeField] private float groundOffset = 0.03f;
    [SerializeField] private SpriteRenderer shadowRenderer;

    private void Awake()
    {
        if (shadowRenderer == null)
        {
            shadowRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            SetVisible(false);
            return;
        }

        RaycastHit2D groundHit = Physics2D.Raycast(
            player.position,
            Vector2.down,
            maxGroundDistance,
            groundLayer);

        if (groundHit.collider == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        transform.position = new Vector3(
            player.position.x,
            groundHit.point.y + groundOffset,
            transform.position.z);
    }

    private void SetVisible(bool visible)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = visible;
        }
    }
}
