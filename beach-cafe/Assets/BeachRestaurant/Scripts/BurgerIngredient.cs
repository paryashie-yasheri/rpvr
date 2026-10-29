using UnityEngine;

public class BurgerIngredient : MonoBehaviour
{
    public enum IngredientKind { BottomBun, Patty, TopBun }
    public IngredientKind kind;

    private void Awake()
    {
        var cookable = GetComponent<StoveCookable>();
        if (!cookable)
        {
            cookable = gameObject.AddComponent<StoveCookable>();
            if (kind != IngredientKind.Patty)
            {
                cookable.readyStart = 0f;
                cookable.readyEnd = 0.1f;
                cookable.smokeStartProgress = 0.1f;
            }
        }

        var gradient = GetComponent<StoveCookableProgressGradient>();
        if (!gradient)
            gameObject.AddComponent<StoveCookableProgressGradient>();

        if (!GetComponent<StoveCookingParticles>())
            gameObject.AddComponent<StoveCookingParticles>();
    }
}
