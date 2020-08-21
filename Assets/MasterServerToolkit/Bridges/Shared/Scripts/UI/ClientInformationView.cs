using Aevien.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public class ClientInformationView : UIView
    {
        private TextMeshProUGUI helpOutput;
        private UIView helpViewSummaryPanel;

        [Header("Components"), SerializeField]
        private TextAsset helpText;

        protected override void Start()
        {
            base.Start();

            helpViewSummaryPanel = ViewsManager.GetView<UIView>("HelpViewSummaryPanel");
            helpOutput = ChildComponent<TextMeshProUGUI>("helpOutput");

            if (helpOutput && helpText)
                helpOutput.text = helpText.text;
        }

        public void ShowHelp()
        {
            if (helpViewSummaryPanel)
                helpViewSummaryPanel.Toggle();
        }
    }
}