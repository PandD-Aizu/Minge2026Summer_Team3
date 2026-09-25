using UnityEngine;
using R3;
using TMPro;
using UnityEngine.UI;

public class DialogueUIView : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private TextMeshProUGUI _name;
    private readonly Subject<Unit> _textBoxClicked = new Subject<Unit>();
    public Observable<Unit> TextBoxClicked => _textBoxClicked;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        _button.onClick.AddListener(OnTextBoxClicked);
    }

    public void SetText(string text)
    {
        _text.text = text;
    }

    public void SetName(string speakerName)
    {
        _name.text = speakerName;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }


    private void OnTextBoxClicked()
    {
        _textBoxClicked.OnNext(Unit.Default);
    }
}
