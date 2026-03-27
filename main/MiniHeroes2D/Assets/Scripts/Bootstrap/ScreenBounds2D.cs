using UnityEngine;

namespace MiniHeroes2D.Bootstrap
{
    public sealed class ScreenBounds2D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float wallThickness = 1f;
        [SerializeField] private float margin = 0.25f;

        private BoxCollider2D leftWall;
        private BoxCollider2D rightWall;
        private BoxCollider2D bottomWall;
        [SerializeField] private bool includeTopWall = false;
        private BoxCollider2D topWall;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            EnsureColliders();
            UpdateWalls();
        }

        private void LateUpdate()
        {
            UpdateWalls();
        }

        private void EnsureColliders()
        {
            leftWall ??= CreateWall("LeftWall");
            rightWall ??= CreateWall("RightWall");
            bottomWall ??= CreateWall("BottomWall");
            if (includeTopWall) topWall ??= CreateWall("TopWall");
            else if (topWall != null) Destroy(topWall.gameObject);
        }

        private BoxCollider2D CreateWall(string name)
        {
            GameObject go = new(name);
            go.transform.SetParent(transform, worldPositionStays: false);

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            return go.AddComponent<BoxCollider2D>();
        }

        private void UpdateWalls()
        {
            if (targetCamera == null) return;
            if (!targetCamera.orthographic) return;

            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;

            Vector3 center = targetCamera.transform.position;
            float left = center.x - (width * 0.5f) + margin;
            float right = center.x + (width * 0.5f) - margin;
            float bottom = center.y - (height * 0.5f) + margin;
            float top = center.y + (height * 0.5f) - margin;

            float wallHeight = height + (wallThickness * 2f);
            float wallWidth = width + (wallThickness * 2f);

            leftWall.size = new Vector2(wallThickness, wallHeight);
            leftWall.offset = new Vector2(left - (wallThickness * 0.5f), center.y);

            rightWall.size = new Vector2(wallThickness, wallHeight);
            rightWall.offset = new Vector2(right + (wallThickness * 0.5f), center.y);

            bottomWall.size = new Vector2(wallWidth, wallThickness);
            bottomWall.offset = new Vector2(center.x, bottom - (wallThickness * 0.5f));

            if (includeTopWall && topWall != null)
            {
                topWall.size = new Vector2(wallWidth, wallThickness);
                topWall.offset = new Vector2(center.x, top + (wallThickness * 0.5f));
            }
        }
    }
}
