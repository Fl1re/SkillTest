using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestWaterStream : Skill
{
    [SerializeField] private float _damagePerTick = 20f;
    [SerializeField] private float _tickInterval = 0.4f;
    [SerializeField] private float _duration = 2f;
    [SerializeField] private float _manaCost = 20f;
    [SerializeField] private float _damageReductionMultiplier = 0.67f;

    [SerializeField] private GameObject _waterStreamPrefab;
    [SerializeField] private float _baseEmissionRate = 100f;

    private GameObject _waterStreamInstance;
    private Vector3 _directionPoint = Vector3.positiveInfinity;
    private ParticleSystem _activeStream;

    protected override int AnimTriggerCastDelay => 0;
    protected override int AnimTriggerCast => 0;

    protected override bool IsCanCast => CheckCanCast();

    private bool CheckCanCast()
    {
        if (float.IsPositiveInfinity(_directionPoint.x)) return false;
        return Vector3.Distance(_directionPoint, transform.position) <= CastLength && NoObstacles(_directionPoint, _obstacle);
    }

    public override void LoadTargetData(TargetInfo targetInfo)
    {
        if (targetInfo.Points.Count > 0)
        {
            _directionPoint = targetInfo.Points[0];
        }
    }

    protected override IEnumerator PrepareJob(Action<TargetInfo> callbackDataSaved)
    {
        while (float.IsPositiveInfinity(_directionPoint.x))
        {
            if (GetMouseButton)
            {
                _directionPoint = GetMousePoint();
            }
            yield return null;
        }

        TargetInfo targetInfo = new TargetInfo();
        targetInfo.Points.Add(_directionPoint);
        callbackDataSaved(targetInfo);
    }

    protected override IEnumerator CastJob()
    {
        if (Moving == Moving.Static)
            _hero.Move.CanMove = false;


        CmdSpawnWaterStream(_directionPoint);
        yield return new WaitUntil(() => _waterStreamInstance != null);
        StartCoroutine(ApplyWaterStreamDamage());
    }

    [Command]
    private void CmdSpawnWaterStream(Vector3 directionPoint)
    {
        Vector3 direction = (directionPoint - transform.position).normalized;

        GameObject streamObj = Instantiate(
            _waterStreamPrefab,
            transform.position,
            Quaternion.LookRotation(direction)
        );
        
        NetworkServer.Spawn(streamObj);
        
        RpcInitializeWaterStream(streamObj);

        StartCoroutine(ServerWaterStreamRoutine(streamObj, direction));
    }

    [ClientRpc]
    private void RpcInitializeWaterStream(GameObject waterObj)
    {
        _waterStreamInstance = waterObj;
    }

    [Server]
    private IEnumerator ServerWaterStreamRoutine(GameObject streamObj, Vector3 direction)
    {
        ParticleSystem stream = streamObj.GetComponent<ParticleSystem>();

        stream.Play();

        float elapsed = 0f;

        while (elapsed < _duration)
        {
            streamObj.transform.position = transform.position;
            streamObj.transform.rotation = Quaternion.LookRotation(direction);

            yield return new WaitForSeconds(_tickInterval);
            elapsed += _tickInterval;
        }

        NetworkServer.Destroy(streamObj);
        _waterStreamInstance = null;
    }

    [Command]
    private void CmdDestroyWaterStream()
    {
        if(_waterStreamInstance != null)
            NetworkServer.Destroy(_waterStreamInstance);
    }

    private IEnumerator ApplyWaterStreamDamage()
    {
        Vector3 direction = (_directionPoint - transform.position).normalized;
        
        float halfAngle = Mathf.Atan((_castWidth / 2f) / CastLength) * Mathf.Rad2Deg;

        ParticleSystem stream = _waterStreamInstance.GetComponent<ParticleSystem>();

        var emission = stream.emission; 
        emission.rateOverTime = _baseEmissionRate;

        float elapsed = 0f;

        while (elapsed < _duration)
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                CastLength,
                TargetsLayers
            );

            List<(float dist, GameObject target)> targets = new();

            foreach (Collider hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                Vector3 toTarget = hit.transform.position - transform.position;
                float distance = toTarget.magnitude;
                if (distance > CastLength || distance <= 0) continue;

                float angle = Vector3.Angle(direction, toTarget.normalized);
                if (angle > halfAngle) continue;

                float proj = Vector3.Dot(toTarget, direction);
                targets.Add((proj, hit.gameObject));
            }

            targets.Sort((a, b) => a.dist.CompareTo(b.dist));

            float currentDamage = _damagePerTick;
            float currentRate = _baseEmissionRate;

            foreach (var (_, target) in targets)
            {
                if (currentDamage <= 0f) break;

                Damage damage = new Damage
                {
                    Value = currentDamage,
                    Type = DamageType.Magical
                };

                CmdApplyDamage(damage, target);

                currentDamage *= _damageReductionMultiplier;
                currentRate *= _damageReductionMultiplier;
            }
            CmdSetEmission(_waterStreamInstance, currentRate);

            yield return new WaitForSeconds(_tickInterval);
            elapsed += _tickInterval;
        }

        CmdDestroyWaterStream();
    }
    
    [Command]
    private void CmdSetEmission(GameObject streamObj, float rate)
    {
        RpcSetEmission(streamObj, rate);
    }
    
    [ClientRpc]
    private void RpcSetEmission(GameObject streamObj, float rate)
    {
        if (streamObj == null) return;

        var ps = streamObj.GetComponent<ParticleSystem>();
        var emission = ps.emission;
        emission.rateOverTime = rate;
    }

    protected override void ClearData()
    {
        _directionPoint = Vector3.positiveInfinity;
    }
}