using UnityEngine;

namespace ElementWar.Net
{
    /// <summary>
    /// 训练 Bot 的运行时可视化：线框使用服务器确认的参数；Game View 面板只向服务器申请调参。
    /// 不依赖 Scene/Prefab 或 Inspector 序列化，退出 PVP 即随远端 Bot 一并销毁。
    /// </summary>
    public sealed class BotCombatDebugView : MonoBehaviour
    {
        private const int ArcSegments = 24;
        private const int CircleSegments = 72;
        private NetClient _net;
        private LineRenderer _fieldLine;
        private LineRenderer _rangeLine;
        private LineRenderer _visionLine;
        private Material _lineMaterial;
        private float _fireRange = 14f;
        private float _visionRange = 24f;
        private float _fieldOfViewDegrees = 120f;
        private float _nextRequestTime;

        public void Setup(NetClient net, float fireRange, float visionRange, float fieldOfViewDegrees)
        {
            _net = net;
            ApplyAuthoritativeSettings(fireRange, visionRange, fieldOfViewDegrees);
            EnsureLines();
        }

        public void ApplyAuthoritativeSettings(float fireRange, float visionRange, float fieldOfViewDegrees)
        {
            if (fireRange > 0f) _fireRange = fireRange;
            if (visionRange >= _fireRange) _visionRange = visionRange;
            if (fieldOfViewDegrees > 0f) _fieldOfViewDegrees = fieldOfViewDegrees;
        }

        private void LateUpdate()
        {
            EnsureLines();
            DrawFieldOfView();
            DrawFireRange();
            DrawOccludedVisionRange();
        }

        private void OnGUI()
        {
            if (_net == null || !_net.Connected) return;
            const float x = 12f, y = 340f, width = 270f;
            GUI.Box(new Rect(x, y, width, 120f), "训练 Bot 调试（服务器权威）");
            GUI.Label(new Rect(x + 10f, y + 25f, 90f, 20f), $"射击距离 {_fireRange:F1}m");
            float requestedRange = GUI.HorizontalSlider(new Rect(x + 105f, y + 30f, 150f, 18f), _fireRange, 1f, 35f);
            GUI.Label(new Rect(x + 10f, y + 53f, 90f, 20f), $"视野距离 {_visionRange:F1}m");
            float requestedVisionRange = GUI.HorizontalSlider(new Rect(x + 105f, y + 58f, 150f, 18f), _visionRange, _fireRange, 35f);
            GUI.Label(new Rect(x + 10f, y + 81f, 90f, 20f), $"视野角 {_fieldOfViewDegrees:F0}°");
            float requestedFov = GUI.HorizontalSlider(new Rect(x + 105f, y + 86f, 150f, 18f), _fieldOfViewDegrees, 10f, 180f);
            requestedVisionRange = Mathf.Max(requestedRange, requestedVisionRange);
            if ((Mathf.Abs(requestedRange - _fireRange) > 0.01f || Mathf.Abs(requestedVisionRange - _visionRange) > 0.01f || Mathf.Abs(requestedFov - _fieldOfViewDegrees) > 0.01f)
                && Time.unscaledTime >= _nextRequestTime)
            {
                _nextRequestTime = Time.unscaledTime + 0.08f;
                _net.RequestBotTuning(requestedRange, requestedVisionRange, requestedFov);
            }
        }

        private void EnsureLines()
        {
            if (_fieldLine != null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _fieldLine = CreateLine("BotFieldOfView_120", new Color(1f, 0.8f, 0.1f, 0.95f));
            _rangeLine = CreateLine("BotFireRange", new Color(1f, 0.2f, 0.15f, 0.95f));
            _visionLine = CreateLine("BotVisionRange_Occluded", new Color(1f, 1f, 1f, 0.95f));
        }

        private LineRenderer CreateLine(string lineName, Color color)
        {
            var lineObject = new GameObject(lineName) { hideFlags = HideFlags.DontSave };
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.material = _lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = line.endWidth = 0.035f;
            line.useWorldSpace = true;
            line.loop = false;
            return line;
        }

        private void DrawFieldOfView()
        {
            if (_fieldLine == null) return;
            Vector3 center = transform.position + Vector3.up * 0.06f;
            float half = _fieldOfViewDegrees * 0.5f;
            _fieldLine.positionCount = ArcSegments + 3;
            _fieldLine.SetPosition(0, center);
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = -half + _fieldOfViewDegrees * i / ArcSegments;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                _fieldLine.SetPosition(i + 1, center + direction.normalized * Mathf.Min(_visionRange, GetVisibleDistance(direction)));
            }
            _fieldLine.SetPosition(ArcSegments + 2, center);
        }

        private void DrawFireRange()
        {
            if (_rangeLine == null) return;
            Vector3 center = transform.position + Vector3.up * 0.05f;
            _rangeLine.positionCount = CircleSegments + 1;
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / CircleSegments;
                _rangeLine.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _fireRange);
            }
        }

        /// <summary>白色边界是 Scene 实际静态 Collider 遮挡后的可视半径；服务器判定仍以 CollisionProfile 为准。</summary>
        private void DrawOccludedVisionRange()
        {
            if (_visionLine == null) return;
            Vector3 center = transform.position + Vector3.up * 0.05f;
            _visionLine.positionCount = CircleSegments + 1;
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / CircleSegments;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                _visionLine.SetPosition(i, center + direction * GetVisibleDistance(direction));
            }
        }

        private float GetVisibleDistance(Vector3 horizontalDirection)
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            RaycastHit[] hits = Physics.RaycastAll(origin, horizontalDirection, _visionRange, ~0, QueryTriggerInteraction.Ignore);
            float nearest = _visionRange;
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                // 角色不遮挡 Bot 视野圈；墙体/掩体才会裁切边界。
                if (hit.collider.GetComponentInParent<PlayerModel>() != null) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            return nearest;
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
