using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GameProject.Persistence.CommonData;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace GameProject.UI
{
    public class WorldCharacterUI : MonoBehaviour
    {
        private const string _PREFIX_STRING = "<color=#00000000>";
        private const string _SUFFIX_STRING = "</color>";
        private static readonly int _PrefixAndSuffixLength = _PREFIX_STRING.Length + _SUFFIX_STRING.Length;

        [SerializeField] private Transform chatBubbleTransformRoot;
        [SerializeField] private TMP_Text MessageBubble;
        [SerializeField] private TMP_Text characterNameDisplayText;
        [SerializeField] private TMP_Text characterCurrencyDisplayText;
        [SerializeField] private GameObject characterCurrencyDisplayRoot;
        [SerializeField, Min(0)] private float timePerCharacter = 0.08f;
        [SerializeField, Min(0)] private float timeOutInSeconds = 10;
        [SerializeField, Min(0)] private float fadeAwayDurationInSeconds = 1;
        [SerializeField] private bool _useRichText = false;
        [SerializeField] private Transform _transform;

        private char[] _charBuffer = Array.Empty<char>();
        private string _textToChatDisplay = "";
        private float _timer;
        private int _characterIndex;
        private bool _invisibleCharacters;

        private void Awake()
        {
            MessageBubble.richText = _useRichText;
            _transform = transform;
        }

        private void OnEnable()
        {
            chatBubbleTransformRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyTestText();
            if ( !_transform )
            {
                _transform = transform;
                this.SetDirtySafe();
            }
        }
#endif
        [Conditional("UNITY_EDITOR")]
        private void ApplyTestText()
        {
            ChatDisplayText = "Hello this is a test";
        }

        public void SetCharacterCurrencyDisplay(in GameCurrency gameCurrency)
        {
            characterCurrencyDisplayText.text = GetShortNumberString(gameCurrency.Value);
        }

        public string GetShortNumberString(uint amount)
        {
            string value;
            if ( amount >= 1000000 )
                value = (amount / 1000000).ToString() + "M";
            else if ( amount >= 1000 )
                value = (amount / 1000).ToString() + "K";
            else
                value = amount.ToString();

            return value;
        }

        public void SetCharacterName(string characterName)
        {
            characterNameDisplayText.text = characterName;
        }

        public string ChatDisplayText {
            get => _textToChatDisplay;
            set {
                _timer = 0;
                _characterIndex = 0;
                _textToChatDisplay = value;
                chatBubbleTransformRoot.localScale = Vector3.one;
                chatBubbleTransformRoot.gameObject.CheckSetActive(true);
                if ( GetTotalTMPTextLength(_textToChatDisplay.Length) <= _charBuffer.Length )
                    return;

                if ( _charBuffer.Length != 0 ) //means the array we are about to forget was rented, we need to return it. 
                {
                    ArrayPool<char>.Shared.Return(_charBuffer);
                }

                //allow reusing the same arrays across other scripts the length of those array may be greater than the requested length
                _charBuffer = ArrayPool<char>.Shared.Rent(GetTotalTMPTextLength(value.Length));
                //copy the rich text prefix using spans which designate a memory block
                //more about spans :
                //https://nishanc.medium.com/an-introduction-to-writing-high-performance-c-using-span-t-struct-b859862a84e4
                if ( _useRichText )
                {
                    Span<char> charBufferSpan = _charBuffer.AsSpan(0, _charBuffer.Length);
                    _PREFIX_STRING.AsSpan().CopyTo(charBufferSpan);
                }
            }
        }

        public void ProcessTextUpdate()
        {
            WriteCharacterPerTime(ChatDisplayText);
        }

        private void WriteCharacterPerTime(string textToDisplay)
        {
            if ( _timer < -timeOutInSeconds )
            {
                chatBubbleTransformRoot.localScale = math.clamp(chatBubbleTransformRoot.localScale - (Vector3.one * (Time.deltaTime / fadeAwayDurationInSeconds)), 0, 1);
                if ( chatBubbleTransformRoot.gameObject.activeSelf && chatBubbleTransformRoot.localScale.x <= 0 )
                {
                    chatBubbleTransformRoot.gameObject.SetActive(false);
                }
                return;
            }

            _timer -= Time.deltaTime;

            if ( _characterIndex >= textToDisplay.Length || _timer > 0f )
                return;

            _timer += timePerCharacter;
            _characterIndex++;
            if ( _useRichText )
            {
                AddTextContent(_PREFIX_STRING.Length, textToDisplay);
                CopySuffixAfterTextContent();
            }
            else
            {
                AddTextContent(0, textToDisplay);
            }
            ApplyTextFromBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopySuffixAfterTextContent()
        {
            _SUFFIX_STRING.AsSpan()
                .CopyTo(_charBuffer.AsSpan(_PREFIX_STRING.Length + _characterIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddTextContent(int startIndex, string textToDisplay)
        {
            textToDisplay.AsSpan(0, _characterIndex)
                .CopyTo(_charBuffer.AsSpan(startIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyTextFromBuffer()
        {
            MessageBubble.SetCharArray(_charBuffer, 0, GetTotalTMPTextLength(_characterIndex));
        }

        private int GetTotalTMPTextLength(int textContentLength)
        {
            return _useRichText ? _PrefixAndSuffixLength + textContentLength : textContentLength;
        }

        public void UpdatePosition(float3 newPosition)
        {
            bool hasMoved = math.lengthsq((float3)_transform.position - newPosition) > .001f;
            if ( hasMoved )
            {
                _transform.position = newPosition;
            }
        }

        public void SetUIForNonStreamer()
        {
            characterCurrencyDisplayRoot.CheckSetActive(true);
        }

        public void SetUIForStreamer()
        {
            characterCurrencyDisplayRoot.SetActive(false);
        }
    }
}