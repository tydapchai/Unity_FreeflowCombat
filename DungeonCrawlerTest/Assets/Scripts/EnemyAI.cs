using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Patrol, Chase, Attack }

    [Header("AI State")]
    public AIState currentState;

    [Header("References")]
    public Transform playerTarget;
    public NavMeshAgent agent;
    public Animator anim;
    private EnemyBase enemyBase;

    [Header("Movement Settings")]
    public float walkSpeed = 2.0f;
    public float chaseSpeed = 5.335f;
    public float speedSmoothTime = 0.1f; // Độ mượt khi tăng/giảm tốc animation
    private float _currentAnimSpeed;
    private float _speedVelocity;

    [Header("Ranges")]
    public float detectionRange = 10f;
    public float attackRange = 2.5f; // Tăng nhẹ để tránh bị vướng Collider
    public float patrolRadius = 8f;

    [Header("Combat")]
    public float attackCooldown = 2f;
    private float lastAttackTime;
    private bool isAttacking = false;

    private Vector3 patrolDestination;
    private float idleTimer;
    public float waitTimeAtPoint = 2f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        enemyBase = GetComponent<EnemyBase>();

        if (playerTarget == null)
            playerTarget = GameObject.FindGameObjectWithTag("Player").transform;

        // Cấu hình NavMesh Agent chuẩn theo ảnh của bạn
        agent.speed = chaseSpeed;
        agent.acceleration = 8f;
        agent.stoppingDistance = attackRange - 0.5f; // Dừng trước khi chạm hẳn để đánh mượt hơn

        currentState = AIState.Idle;
    }

    void Update()
    {
        if (enemyBase != null && enemyBase.isDead)
        {
            agent.isStopped = true;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // Chặn Logic khi đang diễn hoạt đòn đánh (để không bị trượt đi khi đang vung tay)
        if (isAttacking)
        {
            UpdateAnimation(0); // Ép animation về Idle khi đang đánh
            return;
        }

        switch (currentState)
        {
            case AIState.Idle:
                IdleState(distanceToPlayer);
                break;
            case AIState.Patrol:
                PatrolState(distanceToPlayer);
                break;
            case AIState.Chase:
                ChaseState(distanceToPlayer);
                break;
            case AIState.Attack:
                AttackState(distanceToPlayer);
                break;
        }

        // Đồng bộ animation dựa trên vận tốc thực tế của Agent
        UpdateAnimation(agent.velocity.magnitude);
    }

    void UpdateAnimation(float targetSpeed)
    {
        // Làm mượt giá trị Speed để tránh animation thay đổi đột ngột
        _currentAnimSpeed = Mathf.SmoothDamp(_currentAnimSpeed, targetSpeed, ref _speedVelocity, speedSmoothTime);
        anim.SetFloat("Speed", _currentAnimSpeed);
    }

    #region AI States

    void IdleState(float distanceToPlayer)
    {
        agent.isStopped = true;
        if (distanceToPlayer <= detectionRange) { currentState = AIState.Chase; return; }

        idleTimer += Time.deltaTime;
        if (idleTimer >= waitTimeAtPoint)
        {
            SearchNewPatrolPoint();
            currentState = AIState.Patrol;
            idleTimer = 0f;
        }
    }

    void PatrolState(float distanceToPlayer)
    {
        agent.isStopped = false;
        agent.speed = walkSpeed;

        if (distanceToPlayer <= detectionRange) { currentState = AIState.Chase; return; }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            currentState = AIState.Idle;
    }

    void ChaseState(float distanceToPlayer)
    {
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(playerTarget.position);

        if (distanceToPlayer <= attackRange) currentState = AIState.Attack;
        else if (distanceToPlayer > detectionRange + 2f) currentState = AIState.Idle;
    }

    void AttackState(float distanceToPlayer)
    {
        agent.isStopped = true;

        // Xoay mặt về phía Player
        Vector3 dir = (playerTarget.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);

        if (distanceToPlayer > attackRange + 0.5f) // Thêm sai số để tránh nhảy State liên tục
        {
            isAttacking = false;
            currentState = AIState.Chase;
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(PlayAttackAnimation());
            lastAttackTime = Time.time;
        }
    }

    // Dùng Coroutine để quản lý thời gian đánh, tránh mất animation di chuyển sau đó
    System.Collections.IEnumerator PlayAttackAnimation()
    {
        isAttacking = true;
        anim.SetTrigger("Attack");

        // Chờ một khoảng thời gian ngắn để animation Attack thực hiện (tùy độ dài clip đánh của bạn)
        yield return new WaitForSeconds(1.5f);

        isAttacking = false;
    }

    void SearchNewPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius + transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
            agent.SetDestination(hit.position);
    }
    #endregion
}