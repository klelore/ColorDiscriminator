using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BalanceAgent : Agent // 建议改个类名，记得文件名也要改成 BalanceAgent.cs
{
    [Header("把小球拖到这里")]
    public GameObject ball; 
    
    private Rigidbody ballRb;

    // 参数：板子最大能倾斜多少度
    private float maxTiltAngle = 30f; 

    private void Start()
    {
        // 获取球的刚体，因为我们需要获取球的速度
        ballRb = ball.GetComponent<Rigidbody>();
    }

    // 1. 重置：球掉下去后，一切还原
    public override void OnEpisodeBegin()
    {
        // 重置板子的旋转（变平）
        this.transform.rotation = Quaternion.identity;
        this.transform.Rotate(Vector3.right, 0f); // 确保完全平

        // 重置球的位置：放到板子正上方稍微高一点的地方
        ball.transform.position = this.transform.position + new Vector3(0, 0.5f, 0);

        // 重置球的速度：让它静止
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // 【关键技巧】给球一个极小的随机初始推力，防止它开局完美平衡，AI学不到东西
        ball.transform.position += new Vector3(Random.Range(-0.1f, 0.1f), 0, Random.Range(-0.1f, 0.1f));
    }

    // 2. 观察：告诉AI现在的局势
    public override void CollectObservations(VectorSensor sensor)
    {
        // A. 板子现在的倾斜角度 (Z轴和X轴) / maxTiltAngle 是为了归一化到 -1到1 之间
        sensor.AddObservation(this.transform.localRotation.z);
        sensor.AddObservation(this.transform.localRotation.x);

        // B. 球相对于板子的位置 (球偏离中心多远)
        Vector3 relativePos = ball.transform.position - this.transform.position;
        sensor.AddObservation(relativePos.x);
        sensor.AddObservation(relativePos.y);
        sensor.AddObservation(relativePos.z);

        // C. 球的滚动速度
        sensor.AddObservation(ballRb.velocity.x);
        sensor.AddObservation(ballRb.velocity.y);
        sensor.AddObservation(ballRb.velocity.z);
    }

    // 3. 动作：AI 决定怎么歪板子
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 获取神经网络的输出 (-1 到 1)
        float actionZ = actionBuffers.ContinuousActions[0]; // 控制 Z 轴旋转
        float actionX = actionBuffers.ContinuousActions[1]; // 控制 X 轴旋转

        // 执行旋转：直接修改 transform 的欧拉角
        // 这里的逻辑是：在当前角度基础上微调，并限制在最大角度内
        
        Vector3 currentRot = this.transform.rotation.eulerAngles;
        
        // 计算新的倾斜角度 (这里简单处理，直接设为目标角度可能更适合新手理解)
        // 这里的 2f 是旋转力度
        float nextZ = currentRot.z + actionZ * 2f; 
        float nextX = currentRot.x + actionX * 2f;

        // 限制角度，防止板子360度大风车
        // 注意：Unity的角度超过180会变成360，处理起来略麻烦，
        // 这里用简单的 Rotate 模拟力矩效果更直观
        this.transform.Rotate(new Vector3(1, 0, 0), actionX * 2f);
        this.transform.Rotate(new Vector3(0, 0, 1), actionZ * 2f);
        
        // --- 奖励逻辑 ---

        // 判断球是否掉下去了 (假设板子在 y=0 的位置，球掉到 y-1 以下就算输)
        if (ball.transform.position.y < this.transform.position.y - 1.0f)
        {
            SetReward(-1.0f); // 掉了就惩罚
            EndEpisode();     // 游戏重开
        }
        else
        {
            SetReward(0.1f); // 只要球还在板子上，每活一帧就给糖吃
        }
    }
    
    // 手动测试：用键盘控制来看看物理对不对
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = -Input.GetAxis("Horizontal"); // 键盘左右键
        continuousActionsOut[1] = Input.GetAxis("Vertical");   // 键盘上下键
    }
}