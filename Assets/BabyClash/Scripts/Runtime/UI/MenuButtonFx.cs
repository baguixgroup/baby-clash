using UnityEngine;
using UnityEngine.EventSystems;

namespace BabyClash
{
    /// <summary>Makes a menu control feel alive: grows and wobbles when selected, plays click sounds. Unscaled time.</summary>
    public sealed class MenuButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] AudioDirector audioDirector;
        [SerializeField] float selectedScale = 1.12f;
        [SerializeField] float wobbleDegrees = 2.5f;

        bool selected;
        float scale = 1f;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(AudioDirector audio) => audioDirector = audio;

        void OnDisable()
        {
            selected = false;
            scale = 1f;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            if (audioDirector != null)
                audioDirector.Play(SfxId.Click, 1.3f, 0.5f);
        }

        public void OnDeselect(BaseEventData eventData) => selected = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSubmit(BaseEventData eventData) => Click();

        public void OnPointerClick(PointerEventData eventData) => Click();

        void Click()
        {
            scale = selectedScale * 0.85f;
            if (audioDirector != null)
                audioDirector.Play(SfxId.Pickup, 1.1f, 0.8f);
        }

        void Update()
        {
            float target = selected ? selectedScale : 1f;
            scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * scale;
            float wobble = selected ? Mathf.Sin(Time.unscaledTime * 5f) * wobbleDegrees : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, wobble);
        }
    }
}
