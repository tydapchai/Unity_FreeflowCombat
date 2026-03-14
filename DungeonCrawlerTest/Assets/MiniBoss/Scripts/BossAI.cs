using UnityEngine;
using UnityEngine.AI;

public class BossAI : MonoBehaviour
{
    public enum AIState { Idle, Patrol, Chase, Attack, Staggered }

    [Header("AI State")]
    public AIState currentState;

    [Header("References")]
    public Transform playerTarget;
    public NavMeshAgent agent;
    public Animator anim;
    private EnemyBase enemyBase;
    private PlayerHealth playerHealthScript;

    [Header("Movement Settings")]
    public float walkSpeed = 2.0f;
    public float chaseSpeed = 5.0f;
    public float speedSmoothTime = 0.1f;
    private float _currentAnimSpeed;
    private float _speedVelocity;

    [Header("Ranges")]
    public float detectionRange = 12f;
    public float attackRange = 3.0f;
    public float patrolRadius = 10f;

    [Header("Combat Mechanics")]
    public float attackCooldown = 2.5f;
    private float lastAttackTime;
    public bool isAttacking = false;
    public int currentPhase = 1;

    [Header("Stagger Settings")]
    public int maxPoise = 5;
    private int currentPoise;
    public float staggerDuration = 0.8f;
    public float hitRecoveryTime = 0.4f; // Thời gian khựng nhẹ trước khi quay lại Chase

    [Header("Attack Points (Dual Wield)")]
    public Transform rightAttackPoint;
    public Transform leftAttackPoint;
    public float attackRadius = 1.5f;
    public LayerMask playerLayer;

    private float idleTimer;
    private Coroutine knockbackRoutine;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        enemyBase = GetComponent<EnemyBase>();

        currentPoise = maxPoise;

        if (playerTarget == null)
            playerTarget = GameObject.FindGameObjectWithTag("Player").transform;

        if (playerTarget != null)
            playerHealthScript = playerTarget.GetComponent<PlayerHealth>();

        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange - 0.5f;
        currentState = AIState.Idle;
    }

    void Update()
    {
        if (enemyBase != null && enemyBase.isDead)
        {
            agent.isStopped = true;
            return;
        }

        if (playerHealthScript != null && playerHealthScript.isDead)
        {
            StopAI();
            return;
        }

        if (currentPhase == 1 && enemyBase.currentHealth <= enemyBase.maxHealth / 2)
        {
            EnterPhase2();
        }

        // Nếu đang bị choáng nặng (Stagger) hoặc khựng nhẹ (Hit), không chạy logic AI
        if (currentState == AIState.Staggered) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (isAttacking)
        {
            FaceTarget();
            UpdateAnimation(0);
            return;
        }

        switch (currentState)
        {
            case AIState.Idle: IdleState(distanceToPlayer); break;
            case AIState.Patrol: PatrolState(distanceToPlayer); break;
            case AIState.Chase: ChaseState(distanceToPlayer); break;
            case AIState.Attack: AttackState(distanceToPlayer); break;
        }

        UpdateAnimation(agent.velocity.magnitude);
    }

    void UpdateAnimation(float targetSpeed)
    {
        _currentAnimSpeed = Mathf.SmoothDamp(_currentAnimSpeed, targetSpeed, ref _speedVelocity, speedSmoothTime);
        anim.SetFloat("Speed", _currentAnimSpeed);
    }

    #region AI Logic States
    void IdleState(float distanceToPlayer)
    {
        agent.isStopped = true;
        if (distanceToPlayer <= detectionRange) { currentState = AIState.Chase; return; }
        idleTimer += Time.deltaTime;
        if (idleTimer >= 3f) { SearchNewPatrolPoint(); currentState = AIState.Patrol; idleTimer = 0f; }
    }

    void PatrolState(float distanceToPlayer)
    {
        agent.isStopped = false;
        agent.speed = walkSpeed;
        if (distanceToPlayer <= detectionRange) { currentState = AIState.Chase; return; }
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) currentState = AIState.Idle;
    }

    void ChaseState(float distanceToPlayer)
    {
        agent.isStopped = false;
        agent.speed = (currentPhase == 1) ? chaseSpeed : chaseSpeed * 1.3f;
        agent.SetDestination(playerTarget.position);

        if (distanceToPlayer <= attackRange) currentState = AIState.Attack;
        else if (distanceToPlayer > detectionRange + 3f) currentState = AIState.Idle;
    }

    void AttackState(float distanceToPlayer)
    {
        agent.isStopped = true;
        if (distanceToPlayer > attackRange + 0.5f) { currentState = AIState.Chase; return; }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            ExecuteBossAttack();
            lastAttackTime = Time.time;
        }
    }
    #endregion

    void ExecuteBossAttack()
    {
        isAttacking = true;
        int attackChoice = (currentPhase == 1) ? Random.Range(1, 3) : Random.Range(1, 5);
        anim.SetInteger("AttackIndex", attackChoice);
        anim.SetTrigger("Attack");
    }

    void EnterPhase2()
    {
        currentPhase = 2;
        attackCooldown *= 0.7f;
        maxPoise += 2;
        Debug.Log("🔥 BOSS PHASE 2!");
    }

    void StopAI()
    {
        agent.isStopped = true;
        isAttacking = false;
        UpdateAnimation(0);
    }

    // --- HỆ THỐNG NHẬN ĐÒN VÀ KHỰNG ---
    public void TakeHit(Vector3 attackerPos, float force)
    {
        if (enemyBase.isDead) return;

        CancelInvoke("RecoverFromHit");
        // 1. HỦY CHIÊU NGAY LẬP TỨC
        isAttacking = false;

        // 2. DỪNG DI CHUYỂN
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        // 3. GIẢM POISE
        currentPoise--;

        if (currentPoise <= 0)
        {
            // Bị choáng nặng (Stagger) - Có đẩy lùi
            currentPoise = maxPoise;
            StartKnockback(attackerPos, force * 1.2f, 10f, staggerDuration, "Stagger");
        }
        else
        {
            // Khựng nhẹ (Hit Reaction)
            anim.SetTrigger("GetHit");

            // Chuyển tạm sang Staggered để khóa logic Update trong tích tắc
            currentState = AIState.Staggered;
            Invoke("RecoverFromHit", hitRecoveryTime);
        }
    }

    void RecoverFromHit()
    {
        if (enemyBase.isDead) return;

        // Nếu không trong quá trình knockback nặng thì quay lại Chase
        if (knockbackRoutine == null)
        {
            agent.isStopped = false;
            currentState = AIState.Chase;
        }
    }

    private void StartKnockback(Vector3 attackerPos, float force, float friction, float dur, string trigger)
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(attackerPos, force, friction, dur, trigger));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 attackerPos, float force, float friction, float dur, string trigger)
    {
        currentState = AIState.Staggered;
        isAttacking = false;
        agent.isStopped = true;

        if (anim != null) anim.SetTrigger(trigger);

        Vector3 dir = (transform.position - attackerPos).normalized;
        dir.y = 0;
        float f = force;
        while (f > 0.1f)
        {
            agent.Move(dir * f * Time.deltaTime);
            f = Mathf.Lerp(f, 0, Time.deltaTime * friction);
            yield return null;
        }
        yield return new WaitForSeconds(dur);

        knockbackRoutine = null;
        agent.isStopped = false;
        currentState = AIState.Chase;
    }

    // --- CÁC HÀM GỌI TỪ ANIMATION EVENT ---
    public void PerformRightAttack() { DealDamage(rightAttackPoint); }
    public void PerformLeftAttack() { DealDamage(leftAttackPoint); }

    private void DealDamage(Transform p)
    {
        if (!isAttacking || p == null) return; // Nếu đã bị cancel (isAttacking = false) thì không gây dame
        Collider[] hits = Physics.OverlapSphere(p.position, attackRadius, playerLayer);
        foreach (Collider c in hits)
        {
            PlayerHealth ph = c.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(25f);
        }
    }

    public void FinishAttack() { isAttacking = false; }

    void FaceTarget()
    {
        Vector3 dir = (playerTarget.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    void SearchNewPatrolPoint()
    {
        Vector3 rd = Random.insideUnitSphere * patrolRadius + transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(rd, out hit, patrolRadius, 1)) agent.SetDestination(hit.position);
    }
}