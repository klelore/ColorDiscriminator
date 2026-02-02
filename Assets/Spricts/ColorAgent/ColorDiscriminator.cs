using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class ColorDiscriminator : Agent
{
    [Header("模式切换")]
    public bool isTrainingMode = true; // 🔴 勾上=自动训练，不勾=LLM/键盘测试

    [Header("场景设置")]
    public GameObject redBlock;
    public GameObject greenBlock;
    public GameObject blueBlock;

    [Header("移动参数")]
    public float moveSpeed = 5f;
    public float turnSpeed = 150f;

    // 状态记录
    private int currentTargetColorCode;
    private Rigidbody rBody;

    // 🔥 新增：是否正在等待指令（默认 false）
    private bool isWaitingForCommand = false;

    private void Start()
    {
        rBody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 非训练模式下，用键盘模拟 LLM 下达指令
        if (!isTrainingMode)
        {
            // 按下键盘，相当于 LLM 发送了指令，直接调用 SetUserTarget
            if (Input.GetKeyDown(KeyCode.R)) { SetUserTarget(0); Debug.Log(">> 键盘指令：找红色"); }
            if (Input.GetKeyDown(KeyCode.G)) { SetUserTarget(1); Debug.Log(">> 键盘指令：找绿色"); }
            if (Input.GetKeyDown(KeyCode.B)) { SetUserTarget(2); Debug.Log(">> 键盘指令：找蓝色"); }
        }
    }

    // --- 供 LLM 调用的接口 ---
    public void SetUserTarget(int targetIndex)
    {
        // 1. 设置目标
        currentTargetColorCode = targetIndex;

        // 2. 🔥 解锁！让 Agent 开始动
        isWaitingForCommand = false;

        Debug.Log($"指令接收确认：目标已更新为 {targetIndex}，开始行动！");
    }

    // --- 1. 重置 (回合开始) ---
    public override void OnEpisodeBegin()
    {
        // 重置物理状态
        this.transform.localPosition = new Vector3(0, 0.5f, 0);
        this.transform.localRotation = Quaternion.identity;
        rBody.velocity = Vector3.zero;

        // 重置方块位置 (保持你的防重叠逻辑)
        List<Vector3> usedPositions = new List<Vector3>();
        usedPositions.Add(this.transform.localPosition);
        MoveBlockSafe(redBlock, usedPositions);
        MoveBlockSafe(greenBlock, usedPositions);
        MoveBlockSafe(blueBlock, usedPositions);

        // 🔥 核心逻辑分歧
        if (isTrainingMode)
        {
            // 训练模式：直接随机，马上开跑
            currentTargetColorCode = Random.Range(0, 3);
            isWaitingForCommand = false;
        }
        else
        {
            // LLM 模式：重置完环境后，立刻“冻结”
            // 等待 SetUserTarget 被调用后才解冻
            isWaitingForCommand = true;
            Debug.Log("环境已重置，等待指令中...");
        }
    }

    // --- 2. 观察 ---
    public override void CollectObservations(VectorSensor sensor)
    {
        // 即使在等待中，也可以发观察数据（反正动不了），或者发全0
        if (currentTargetColorCode == 0) { sensor.AddObservation(1); sensor.AddObservation(0); sensor.AddObservation(0); }
        else if (currentTargetColorCode == 1) { sensor.AddObservation(0); sensor.AddObservation(1); sensor.AddObservation(0); }
        else { sensor.AddObservation(0); sensor.AddObservation(0); sensor.AddObservation(1); }
    }

    // --- 3. 动作 ---
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 🔥 冻结逻辑：如果没有收到指令，什么都不做
        if (!isTrainingMode && isWaitingForCommand)
        {
            rBody.velocity = Vector3.zero; // 确保停住
            return; // 直接跳出，不执行后面的移动代码
        }

        float moveSignal = actionBuffers.ContinuousActions[0];
        float rotateSignal = actionBuffers.ContinuousActions[1];

        Vector3 moveForce = transform.forward * moveSignal * moveSpeed;
        rBody.velocity = new Vector3(moveForce.x, rBody.velocity.y, moveForce.z);
        transform.Rotate(0, rotateSignal * turnSpeed * Time.fixedDeltaTime, 0);

        // 只有动的时候才扣分
        SetReward(-0.001f);
    }

    // --- 4. 碰撞检测 ---
    private void OnCollisionEnter(Collision collision)
    {
        // 如果在等待指令期间（理论上动不了，以防万一）被方块撞了，不处理
        if (!isTrainingMode && isWaitingForCommand) return;

        string hitTag = collision.gameObject.tag;

        if (IsCorrectTarget(hitTag))
        {
            SetReward(1.0f);
            EndEpisode(); // 结束回合 -> 触发 OnEpisodeBegin -> 再次冻结等待下一条指令
            Debug.Log("Good AI! Found " + hitTag);
        }
        else if (hitTag == "Wall")
        {
            SetReward(-0.5f);
        }
        else if (IsWrongTarget(hitTag))
        {
            SetReward(-1.0f);
            EndEpisode(); // 结束回合 -> 再次冻结
            Debug.Log("Bad AI! Hit wrong color: " + hitTag);
        }
    }

    bool IsCorrectTarget(string tag)
    {
        if (currentTargetColorCode == 0 && tag == "Red") return true;
        if (currentTargetColorCode == 1 && tag == "Green") return true;
        if (currentTargetColorCode == 2 && tag == "Blue") return true;
        return false;
    }

    bool IsWrongTarget(string tag)
    {
        return (tag == "Red" || tag == "Green" || tag == "Blue") && !IsCorrectTarget(tag);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 手动测试时，允许用键盘控制移动（可选）
        // 如果你想完全模拟 AI，可以把这里留空，只靠 SetUserTarget 触发
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Vertical");
        continuousActions[1] = Input.GetAxis("Horizontal");
    }

    void MoveBlockSafe(GameObject block, List<Vector3> usedPositions)
    {
        Vector3 newPos = Vector3.zero;
        bool positionFound = false;
        int attempts = 0;
        float safeRadius = 1.5f;

        while (!positionFound && attempts < 100)
        {
            attempts++;
            newPos = new Vector3(Random.Range(-4f, 4f), 0.5f, Random.Range(-4f, 4f));
            bool tooClose = false;
            foreach (Vector3 p in usedPositions)
            {
                if (Vector3.Distance(newPos, p) < safeRadius)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) positionFound = true;
        }
        block.transform.localPosition = newPos;
        usedPositions.Add(newPos);
    }
}