using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class ShiBie : Agent
{

    public GameObject target;
    public float rSpeed = 1;

    private Rigidbody rBody;

    private void Start()
    {
        rBody = GetComponent<Rigidbody>();
    }

    // 1. 初始化与重置：每当开始新的一轮训练(Episode)时调用
    public override void OnEpisodeBegin()
    {
        // 重置位置，防止掉出地图
        if (this.transform.localPosition.y < 0)
        {
            this.transform.localPosition = new Vector3(0, 0.5f, 0);
        }
        // 随机移动目标位置，增加训练难度
        target.transform.localPosition = new Vector3(Random.value * 8 - 4, 0.5f, Random.value * 8 - 4);
    }

    // 2. 收集观察：告诉AI现在的状态
    public override void CollectObservations(VectorSensor sensor)
    {
        // 传入目标的坐标 (3个数据)
        sensor.AddObservation(target.transform.localPosition);
        // 传入自己的坐标 (3个数据)
        sensor.AddObservation(this.transform.localPosition);

        // 也可以传入速度、角度等
    }

    // 3. 执行动作与接收反馈：AI的大脑算出动作后，这里负责执行
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 接收神经网络输出的数值
        float moveX = actionBuffers.ContinuousActions[0];
        float moveZ = actionBuffers.ContinuousActions[1];

        // 物理操作：施加力
        rBody.AddForce(new Vector3(moveX, 0, moveZ) * rSpeed);

        // --- 设计奖励函数 (关键) ---

        // 这里的距离是 AI 判断是否成功的依据
        float distanceTotarget = Vector3.Distance(this.transform.localPosition, target.transform.localPosition);

        // 离得越近，给一点微小的奖励 (引导)
        // SetReward(-0.001f); // 或者给时间惩罚，迫使它通过最短路径完成

        if (distanceTotarget < 1.42f)
        {
            SetReward(1.0f); // 成功到达！给大奖励
            EndEpisode();    // 结束这一轮
        }
        else if (this.transform.localPosition.y < 0)
        {
            SetReward(-1.0f); // 掉下悬崖，给大惩罚
            EndEpisode();     // 结束这一轮
        }
    }
}