using System;
using System.Reflection;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(BehaviorParameters))]
[RequireComponent(typeof(DecisionRequester))]
public class GruntAgent : Agent
{
    private const float DistanceNormalization = 10f;

    private GruntScript grunt;
    private Transform player;
    private float lastDistanceToPlayer;
    private bool configured;

    public void Initialize(GruntScript gruntScript, Transform playerTransform)
    {
        grunt = gruntScript;
        player = playerTransform;
        ConfigureAgentComponents();
        lastDistanceToPlayer = GetDistanceToPlayer();
    }

    public override void OnEpisodeBegin()
    {
        lastDistanceToPlayer = GetDistanceToPlayer();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (grunt == null || player == null)
        {
            for (int i = 0; i < 7; i++)
            {
                sensor.AddObservation(0f);
            }
            return;
        }

        Vector2 delta = player.position - transform.position;
        sensor.AddObservation(Mathf.Clamp(delta.x / DistanceNormalization, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(delta.y / DistanceNormalization, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(delta.magnitude / DistanceNormalization, 0f, 1f));
        sensor.AddObservation(grunt.CurrentHealthRatio);
        sensor.AddObservation(grunt.IsPlayerInShootRange ? 1f : 0f);
        sensor.AddObservation(grunt.IsPlayerInChaseRange ? 1f : 0f);
        sensor.AddObservation(grunt.FacingPlayer ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (grunt == null || grunt.IsDead)
        {
            return;
        }

        int moveAction = actions.DiscreteActions.Length > 0 ? actions.DiscreteActions[0] : 0;
        int shootAction = actions.DiscreteActions.Length > 1 ? actions.DiscreteActions[1] : 0;

        float moveInput = 0f;
        if (moveAction == 1)
        {
            moveInput = -1f;
        }
        else if (moveAction == 2)
        {
            moveInput = 1f;
        }

        grunt.SetMoveInput(moveInput);
        if (shootAction == 1)
        {
            grunt.TryShoot(true);
        }

        float currentDistance = GetDistanceToPlayer();
        AddReward(currentDistance < lastDistanceToPlayer ? 0.0025f : -0.001f);
        AddReward(-0.0005f);
        lastDistanceToPlayer = currentDistance;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        if (grunt == null || player == null)
        {
            for (int i = 0; i < discreteActions.Length; i++)
            {
                discreteActions[i] = 0;
            }
            return;
        }

        float deltaX = player.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= grunt.StopDistance)
        {
            discreteActions[0] = 0;
        }
        else if (deltaX < 0f)
        {
            discreteActions[0] = 1;
        }
        else
        {
            discreteActions[0] = 2;
        }

        if (discreteActions.Length > 1)
        {
            discreteActions[1] = grunt.IsPlayerInShootRange ? 1 : 0;
        }
    }

    public void NotifySuccessfulAttack()
    {
        AddReward(0.35f);
    }

    public void NotifyDamageTaken()
    {
        AddReward(-0.1f);
    }

    public void NotifyDeath()
    {
        AddReward(-1f);
        EndEpisode();
    }

    public void NotifyRespawn()
    {
        lastDistanceToPlayer = GetDistanceToPlayer();
    }

    private void ConfigureAgentComponents()
    {
        if (configured)
        {
            return;
        }

        BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();
        ConfigureBehaviorParameters(behaviorParameters);

        DecisionRequester decisionRequester = GetComponent<DecisionRequester>();
        SetMemberValue(decisionRequester, "DecisionPeriod", 5);
        SetMemberValue(decisionRequester, "DecisionStep", 0);
        SetMemberValue(decisionRequester, "TakeActionsBetweenDecisions", true);

        MaxStep = 0;
        configured = true;
    }

    private static void ConfigureBehaviorParameters(BehaviorParameters behaviorParameters)
    {
        SetMemberValue(behaviorParameters, "BehaviorName", "MiniHeroesGrunt");
        SetEnumMemberValue(behaviorParameters, "BehaviorType", "Default");
        SetMemberValue(behaviorParameters, "TeamId", 1);
        SetMemberValue(behaviorParameters, "UseChildSensors", true);
        SetMemberValue(behaviorParameters, "UseChildActuators", true);

        object brainParameters = GetMemberValue(behaviorParameters, "BrainParameters");
        if (brainParameters == null)
        {
            return;
        }

        SetMemberValue(brainParameters, "VectorObservationSize", 7);
        SetMemberValue(brainParameters, "NumStackedVectorObservations", 1);

        MemberInfo actionSpecMember = FindMember(brainParameters.GetType(), "ActionSpec");
        if (actionSpecMember != null)
        {
            Type actionSpecType = GetMemberType(actionSpecMember);
            MethodInfo makeDiscreteMethod = actionSpecType.GetMethod("MakeDiscrete", BindingFlags.Public | BindingFlags.Static);
            if (makeDiscreteMethod != null)
            {
                object actionSpec = makeDiscreteMethod.Invoke(null, new object[] { new[] { 3, 2 } });
                SetMemberValue(brainParameters, "ActionSpec", actionSpec);
            }
        }
        else
        {
            SetEnumMemberValue(brainParameters, "VectorActionSpaceType", "Discrete");
            SetMemberValue(brainParameters, "VectorActionSize", new[] { 3, 2 });
        }

        SetMemberValue(behaviorParameters, "BrainParameters", brainParameters);
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null)
        {
            return null;
        }

        MemberInfo member = FindMember(target.GetType(), memberName);
        if (member is PropertyInfo propertyInfo)
        {
            return propertyInfo.GetValue(target);
        }

        if (member is FieldInfo fieldInfo)
        {
            return fieldInfo.GetValue(target);
        }

        return null;
    }

    private static void SetMemberValue(object target, string memberName, object value)
    {
        if (target == null)
        {
            return;
        }

        MemberInfo member = FindMember(target.GetType(), memberName);
        if (member is PropertyInfo propertyInfo && propertyInfo.CanWrite)
        {
            propertyInfo.SetValue(target, value);
            return;
        }

        if (member is FieldInfo fieldInfo)
        {
            fieldInfo.SetValue(target, value);
        }
    }

    private static void SetEnumMemberValue(object target, string memberName, string enumValue)
    {
        if (target == null)
        {
            return;
        }

        MemberInfo member = FindMember(target.GetType(), memberName);
        if (member == null)
        {
            return;
        }

        Type enumType = GetMemberType(member);
        if (!enumType.IsEnum)
        {
            return;
        }

        object parsedValue = Enum.Parse(enumType, enumValue);
        SetMemberValue(target, memberName, parsedValue);
    }

    private static MemberInfo FindMember(Type type, string memberName)
    {
        MemberInfo member = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? (MemberInfo)type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (member != null)
        {
            return member;
        }

        string alternateName = char.ToLowerInvariant(memberName[0]) + memberName.Substring(1);
        return type.GetProperty(alternateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? (MemberInfo)type.GetField(alternateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static Type GetMemberType(MemberInfo member)
    {
        if (member is PropertyInfo propertyInfo)
        {
            return propertyInfo.PropertyType;
        }

        return ((FieldInfo)member).FieldType;
    }

    private float GetDistanceToPlayer()
    {
        return player == null ? DistanceNormalization : Vector2.Distance(transform.position, player.position);
    }
}
