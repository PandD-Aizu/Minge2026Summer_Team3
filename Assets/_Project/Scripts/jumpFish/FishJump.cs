using UnityEngine;

public class FishJump : MonoBehaviour
{
    [Header("ジャンプ設定")]
    public float jumpHeight = 1.5f;
    public float jumpDistance = 1.5f;
    public float jumpDuration = 1.2f;

    [Header("次のジャンプまで")]
    public float minWaitTime = 3f;
    public float maxWaitTime = 6f;

    private Vector3 startPosition;

    private float jumpTimer = 0f;
    private float waitTimer = 0f;
    private float nextJumpTime;

    private bool isJumping = false;

    // 魚本体と子オブジェクトのSpriteRendererを全部取得
    private MeshRenderer _renderer;

    void Start()
    {
        // 最初の位置を記憶
        startPosition = transform.position;

        // 自分自身＋子オブジェクトのSpriteRendererを取得
        _renderer = GetComponent<MeshRenderer>();

        // 最初のジャンプまでの時間
        nextJumpTime = Random.Range(
            minWaitTime,
            maxWaitTime
        );
    }

    void Update()
    {
        if (isJumping)
        {
            Jump();
            return;
        }

        waitTimer += Time.deltaTime;

        if (waitTimer >= nextJumpTime)
        {
            StartJump();
        }
    }

    void StartJump()
    {
        isJumping = true;
        jumpTimer = 0f;
        waitTimer = 0f;

        // 最初の位置からジャンプ
        transform.position = startPosition;

        // 魚を表示
        SetFishVisible(true);
    }

    void Jump()
    {
        jumpTimer += Time.deltaTime;

        float t = jumpTimer / jumpDuration;

        // 斜め上 → 斜め下
        float horizontal = t * jumpDistance;
        float height = Mathf.Sin(t * Mathf.PI) * jumpHeight;

        transform.position = new Vector3(
            startPosition.x + horizontal,
            startPosition.y + height,
            startPosition.z
        );

        // 着水
        if (t >= 1f)
        {
            // 最初の位置に戻す
            transform.position = startPosition;

            isJumping = false;
            jumpTimer = 0f;

            // 魚を非表示
            SetFishVisible(false);

            // 次のジャンプまで待つ
            nextJumpTime = Random.Range(
                minWaitTime,
                maxWaitTime
            );
        }
    }

    void SetFishVisible(bool visible)
    {
        _renderer.enabled = visible;
    }
}
