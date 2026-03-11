using UnityEngine;
using UnityEngine.UI;

public class SimpleUITester : MonoBehaviour
{
    [Header("UI References (Kéo từ UIStat sang)")]
    public Image statBar;      // Thanh máu chính
    public Image statBarFade;  // Thanh máu mờ (chạy sau)
    public Text currentValueText; // Chữ hiển thị số máu hiện tại

    [Header("Settings")]
    public float maxHealth = 100f;
    public float damagePerSecond = 10f;

    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        // 1. Giảm máu theo thời gian
        if (currentHealth > 0)
        {
            currentHealth -= damagePerSecond * Time.deltaTime;
        }
        else
        {
            currentHealth = maxHealth; // Reset lại khi hết máu để test tiếp
        }

        // 2. Tính toán tỷ lệ (0.0 đến 1.0)
        float ratio = currentHealth / maxHealth;

        // 3. Cập nhật thanh máu chính (giảm ngay lập tức)
        if (statBar != null)
        {
            statBar.fillAmount = ratio;
        }

        // 4. Cập nhật thanh Fade (giảm từ từ - giống logic trong code của bạn)
        if (statBarFade != null)
        {
            statBarFade.fillAmount = Mathf.MoveTowards(statBarFade.fillAmount, ratio, Time.deltaTime * 0.5f);
        }

        // 5. Cập nhật chữ số
        if (currentValueText != null)
        {
            currentValueText.text = Mathf.CeilToInt(currentHealth).ToString();
        }
    }
}