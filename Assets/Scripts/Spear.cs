using UnityEngine;

public class Spear : MonoBehaviour
{
    public enum SpearState { Flying, Embedded, Returning }
    public SpearState currentState = SpearState.Flying;

    [Header("Settings")]
    [SerializeField] private float returnSpeed = 25f;
    [SerializeField] private int impactDamage = 1;
    [SerializeField] private int returnDamage = 2;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private Transform _playerTransform;
    private PlayerCombat _playerCombat;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != SpearState.Flying) return;
        
        gameObject.layer = LayerMask.NameToLayer("Spear");
        foreach (Transform child in gameObject.transform)
        {
            gameObject.layer = LayerMask.NameToLayer("Spear");
        }

        if (collision.gameObject.CompareTag("Enemy"))
        {
            Enemy enemy = collision.gameObject.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(impactDamage);
                StickToTarget(collision.transform, true);
            }
        }
        else if (Mathf.Abs(collision.contacts[0].normal.x) > 0.7f)
        {
            StickToTarget(null, false, collision.contacts[0]);
        }
        else 
        {
            StopPhysics();
        }
    }

    void StickToTarget(Transform target, bool isEnemy, ContactPoint2D contact = default)
    {
        currentState = SpearState.Embedded;
        StopPhysics();

        if (isEnemy)
        {
            transform.SetParent(target);
        }
        else
        {
            transform.rotation = Quaternion.identity;
            transform.position = contact.point;
        }
    }

    void StopPhysics()
    {
        _rb.bodyType = RigidbodyType2D.Static;
        _collider.isTrigger = false;
    }

    public void StartReturn(Transform player, PlayerCombat combat)
    {
        _playerTransform = player;
        _playerCombat = combat;
        
        currentState = SpearState.Returning;
        transform.SetParent(null);
        _rb.bodyType = RigidbodyType2D.Kinematic; 
        _rb.linearVelocity = Vector2.zero;
        _collider.isTrigger = true;
    }

    void Update()
    {
        if (currentState == SpearState.Returning)
        {
            MoveTowardsPlayer();
        }
    }

    void MoveTowardsPlayer()
    {
        Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * returnSpeed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (Vector2.Distance(transform.position, _playerTransform.position) < 0.5f)
        {
            _playerCombat.CatchSpear(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (currentState == SpearState.Returning && collision.CompareTag("Enemy"))
        {
            Enemy enemy = collision.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(returnDamage);
        }
    }
}