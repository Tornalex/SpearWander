using UnityEngine;

public class Spear : MonoBehaviour
{
    public enum SpearState { Flying, Embedded, Returning }
    public SpearState currentState = SpearState.Flying;

    [Header("Settings")]
    [SerializeField] float returnSpeed = 25f;
    [SerializeField] int returnDamage = 2;

    private Rigidbody2D _rb;
    private Transform _playerTransform;
    private PlayerCombat _playerCombat;
    private Collider2D _collider;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
    }

    public void StartReturn(Transform player, PlayerCombat combat)
    {
        _playerTransform = player;
        _playerCombat = combat;
        
        // Prepariamo la lancia per il volo di ritorno
        currentState = SpearState.Returning;
        _rb.bodyType = RigidbodyType2D.Kinematic; // Disattiva gravità e attrito
        _rb.linearVelocity = Vector2.zero;
        _collider.isTrigger = true; // Attraversa i muri, ma può colpire nemici
        transform.SetParent(null); // Si stacca da muri o nemici
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
        // Calcoliamo la direzione verso il giocatore
        Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        
        // Applichiamo la velocità
        _rb.linearVelocity = direction * returnSpeed;

        // Ruotiamo la lancia affinché la punta guardi verso il player (opzionale, fa molto GoW)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Controllo distanza per il "riaggancio"
        if (Vector2.Distance(transform.position, _playerTransform.position) < 0.5f)
        {
            _playerCombat.CatchSpear(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Se stiamo tornando e colpiamo un nemico
        if (currentState == SpearState.Returning && collision.CompareTag("Enemy"))
        {
            Enemy enemy = collision.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(returnDamage);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == SpearState.Flying)
        {
            // Tua logica originale per conficcarsi nei muri/nemici
            StopSpear(collision);
        }
    }

    void StopSpear(Collision2D collision)
    {
        currentState = SpearState.Embedded;
        _rb.bodyType = RigidbodyType2D.Static; // La blocca completamente
        if(collision.transform.CompareTag("Enemy"))
            transform.SetParent(collision.transform);
    }
}