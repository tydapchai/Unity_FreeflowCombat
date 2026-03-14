using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("Health System")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isDead = false;

    [Header("Components")]
    public Animator anim;
    public Collider enemyCollider;

    [Header("VFX & UI")]
    [SerializeField] private GameObject hitVfx;
    [SerializeField] private GameObject activeTargetObject;

    // References tới các loại AI
    private EnemyAI enemyAI;
    private BossAI bossAI;

    void Start()
    {
        ActiveTarget(false);
        currentHealth = maxHealth;

        if (anim == null) anim = GetComponent<Animator>();
        if (enemyCollider == null) enemyCollider = GetComponent<Collider>();

        // Tìm các script AI đi kèm
        enemyAI = GetComponent<EnemyAI>();
        bossAI = GetComponent<BossAI>();
    }

    public void TakeDamage(float damageAmount, bool playDefaultHitReaction = true)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        Debug.Log(gameObject.name + " nhận " + damageAmount + " sát thương! Máu còn: " + currentHealth);

        // --- PHẦN SỬA CHÍNH: ĐIỀU PHỐI HIỆU ỨNG TRÚNG ĐÒN ---

        // 1. Lấy vị trí Player để tính hướng văng (Knockback)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 attackerPos = (player != null) ? player.transform.position : transform.position;

        // 2. Nếu là Boss: Gọi TakeHit trong BossAI để hủy chiêu và khựng
        if (bossAI != null)
        {
            bossAI.TakeHit(attackerPos, 5f); // 5f là lực đẩy mặc định
        }
        // 3. Nếu là Quái thường: Gọi TakeHit trong EnemyAI
        else if (enemyAI != null)
        {
            enemyAI.TakeHit(attackerPos, 4f);
        }
        // 4. Nếu không có AI xịn (quái đứng yên): Chạy GetHit đơn giản
        else if (playDefaultHitReaction && anim != null)
        {
            anim.SetTrigger("GetHit");
        }

        // Kiểm tra chết
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        Debug.Log(gameObject.name + " đã bị tiêu diệt!");

        if (anim != null)
        {
            // Đồng bộ với Animator mới: dùng bool isDead
            anim.SetBool("isDead", true);
        }

        if (enemyCollider != null) enemyCollider.enabled = false;

        ActiveTarget(false);
        Destroy(gameObject, 3f);
    }

    public void SpawnHitVfx(Vector3 Pos_)
    {
        if (hitVfx != null) Instantiate(hitVfx, Pos_, Quaternion.identity);
    }

    public void ActiveTarget(bool bool_)
    {
        if (activeTargetObject != null) activeTargetObject.SetActive(bool_);
    }
}