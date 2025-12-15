using System.Collections;
using UnityEngine;

public class LineConusRenderer : MonoBehaviour
{
    [SerializeField] private LineRenderer _lineRenderer;

    private Skill _skill;
    private Coroutine _coneDrawCoroutine;

    public void StartDraw(Skill skill)
    {
        _skill = skill;
        _lineRenderer.positionCount = 4;
        _coneDrawCoroutine = StartCoroutine(DrawConeJob());
    }

    public void StopDraw()
    {
        if (_coneDrawCoroutine != null)
        {
            StopCoroutine(_coneDrawCoroutine);
            _coneDrawCoroutine = null;
        }
        _lineRenderer.positionCount = 0;
        _skill = null;
    }

    private IEnumerator DrawConeJob()
    {
        while (true)
        {
            Vector3 mousePoint = _skill.GetMousePoint();
            if (mousePoint == Vector3.zero)
            {
                yield return null;
                continue;
            }

            Vector3 direction = (mousePoint - _skill.transform.position).normalized;
            Vector3 endPoint = _skill.transform.position + direction * _skill.CastLength;

            Vector3 perpendicular = new Vector3(-direction.z, 0, direction.x).normalized;
            Vector3 leftEnd = endPoint - perpendicular * (_skill.CastWidth / 2f);
            Vector3 rightEnd = endPoint + perpendicular * (_skill.CastWidth / 2f);

            _lineRenderer.SetPosition(0, _skill.transform.position + Vector3.up * 0.1f);
            _lineRenderer.SetPosition(1, leftEnd + Vector3.up * 0.1f);
            _lineRenderer.SetPosition(2, rightEnd + Vector3.up * 0.1f);
            _lineRenderer.SetPosition(3, _skill.transform.position + Vector3.up * 0.1f);

            yield return null;
        }
    }
}
