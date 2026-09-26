using System;
using System.Linq;
using Reconnect.Client.Economy;
using Reconnect.Contracts.RealEstate;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>
    /// Offices of a tower for sale (and the ones I own): buy with in-game francs, sell back for the price.
    /// Buying asks for a second tap ("Wirklich kaufen?") instead of a dialog.
    /// </summary>
    public sealed class OfficesScreen : ScreenBase
    {
        private readonly VisualTreeAsset _template;
        private readonly RealEstateService _realEstate;
        private readonly WalletService _wallet;
        private readonly Guid _buildingId;
        private readonly Action _back;
        private Guid? _confirming;

        public OfficesScreen(VisualTreeAsset template, RealEstateService realEstate, WalletService wallet, Guid buildingId, Action back)
        {
            _template = template;
            _realEstate = realEstate;
            _wallet = wallet;
            _buildingId = buildingId;
            _back = back;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            Q<Button>("back").clicked += _back;
            RunAsync(LoadAsync);
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            var status = Q<Label>("status");
            SetStatus(status, "Lade Büros …", isError: false);
            var wallet = await _wallet.GetAsync(Lifetime);
            var offices = await _realEstate.GetOfficesAsync(_buildingId, Lifetime);
            if (!offices.IsSuccess)
            {
                SetStatus(status, offices.Error.ToDisplayString());
                return;
            }
            var balance = wallet.IsSuccess ? wallet.Value.Balance : 0m;
            Q<Label>("balance").text = Money.Format(balance);
            SetStatus(status, null);

            var list = Q<ScrollView>("offices");
            list.Clear();
            var mine = offices.Value.Where(o => o.IsMine).ToList();
            if (mine.Count > 0)
            {
                list.Add(Header("Deine Büros"));
                foreach (var office in mine)
                {
                    list.Add(Row(office, balance));
                }
            }
            foreach (var floor in offices.Value.Where(o => o.IsAvailable).GroupBy(o => o.Floor).OrderByDescending(g => g.Key))
            {
                list.Add(Header(floor.Key + ". OG"));
                foreach (var office in floor)
                {
                    list.Add(Row(office, balance));
                }
            }
        }

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.AddToClassList("office-floor-header");
            return label;
        }

        private VisualElement Row(OfficeUnitDto office, decimal balance)
        {
            var row = new VisualElement();
            row.AddToClassList("office-row");
            row.EnableInClassList("office-row--mine", office.IsMine);

            var text = new VisualElement();
            text.AddToClassList("office-row__text");
            var title = new Label(office.IsMine ? "★ " + office.Name : office.Name);
            title.AddToClassList("office-row__title");
            var details = new Label($"{office.Floor}. OG · {office.AreaSquareMeters} m²" + (office.IsMine ? " · im Lift erreichbar" : ""));
            details.AddToClassList("office-row__details");
            var price = new Label(Money.Format(office.Price));
            price.AddToClassList("office-row__price");
            text.Add(title);
            text.Add(details);
            text.Add(price);
            row.Add(text);

            var button = new Button { text = office.IsMine ? "Verkaufen" : _confirming == office.Id ? "Wirklich kaufen?" : "Kaufen" };
            button.AddToClassList("button");
            button.AddToClassList("button--small");
            if (!office.IsMine)
            {
                button.AddToClassList("button--primary");
                button.SetEnabled(office.Price <= balance);
            }
            button.clicked += () => Act(office);
            row.Add(button);
            return row;
        }

        private void Act(OfficeUnitDto office)
        {
            if (!office.IsMine && _confirming != office.Id)
            {
                _confirming = office.Id;   // second tap buys
                RunAsync(LoadAsync);
                return;
            }
            _confirming = null;
            RunAsync(async () =>
            {
                var result = office.IsMine
                    ? await _realEstate.SellAsync(office.Id, Lifetime)
                    : await _realEstate.BuyAsync(office.Id, Lifetime);
                await LoadAsync();
                SetStatus(Q<Label>("status"), result.IsSuccess
                    ? office.IsMine
                        ? $"Verkauft – {Money.Format(office.Price)} gutgeschrieben."
                        : $"Gekauft! Dein Büro im {office.Floor}. OG erreichst du im Prime Tower mit dem Lift."
                    : result.Error.ToDisplayString(), isError: !result.IsSuccess);
            });
        }
    }
}
