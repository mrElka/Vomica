using UnityEngine;
using UnityEngine.EventSystems;

public class HoverImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject imageToShow;

    private void OnEnable()
    {
        if (imageToShow != null)
            imageToShow.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (imageToShow != null)
            imageToShow.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (imageToShow != null)
            imageToShow.SetActive(false);
    }
}