using Content.Client._Stories.Security.JAS.Layouts.Home;
using Content.Client._Stories.Security.JAS.Layouts.Main;
using Content.Client._Stories.Security.JAS.UiComponents;
using Content.Client._Stories.Security.JAS.Views.Charges;
using Content.Client._Stories.Security.JAS.Views.Details;
using Content.Shared._Stories.Security.JAS;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Stories.Security.JAS;

/* TODO:
    - Нормальное формирование списка статьей с сохранением состояния для консоли и их отображение
    - Отправка ВЫБРАННЫХ СТАТЬЕЙ, ВЫБРАННЫХ УЛИК, ВЫБРАННЫХ СВИДЕТЕЛЕЙ, ОБВИНИТЕЛЯ, ОБВИНЕМОГО как форма на SharedJASystem с формированием отчёта для консоли и таймера
    - Переписать ебучие JASBUI (я заебался от срача)
 */

public sealed class JASBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly HashSet<JASChargeCard> _variableCharges = [];
    private readonly HashSet<JASChargeCard> _minorCharges = [];
    private readonly HashSet<JASChargeCard> _majorCharges = [];
    private readonly HashSet<JASChargeCard> _capitalCharges = [];
    private readonly HashSet<JASChargeCard> _optionalCharges = [];

    private JASWindow? _window;

    private JASHomeLayout? _homeLayout;
    private JASMainLayout? _mainLayout;

    private JASDetailsView? _detailsView;
    private JASChargesView? _chargesView;

    public JASBui (EntityUid owner, Enum uiKey)  : base (owner, uiKey) {}


    protected override void Open()
    {
        if (!EntMan.TryGetComponent(Owner, out JASComponent? jasComp))
            return;

        base.Open();

        _window = this.CreateWindow<JASWindow>();
        _homeLayout = new JASHomeLayout();

        _window.JASHomeLayoutWrapper.AddChild(_homeLayout);
        _homeLayout.JASLoginButton.OnPressed += _ => Login();
    }

    protected override void UpdateState(BoundUserInterfaceState rawState)
    {
        base.UpdateState(rawState);

        if (rawState is not JASBuiState state)
            return;

        UpdateWindow(state);
    }

    private void UpdateWindow(JASBuiState state)
    {
        if (_window == null)
            return;

        if (state.Authorized && _mainLayout == null)
        {
            if (_homeLayout == null)
                return;

            _homeLayout.JASHomeLayoutContainer.Visible = false;
            PopulateMainLayout();

            if (state.Tab != JASTabs.None)
                ShowTab(state.Tab);

            if (!string.IsNullOrEmpty(state.Details) && _detailsView != null)
                _detailsView.JASDetailsViewInput.TextRope = new Rope.Leaf(state.Details);
        }
        else if (!state.Authorized && _homeLayout != null)
        {
            _mainLayout = null;
            _window.JASMainLayoutWrapper.RemoveAllChildren();
            _homeLayout.JASHomeLayoutContainer.Visible = true;
        }


    }

    private void PopulateMainLayout()
    {
        if (_window == null)
            return;

        _mainLayout = new JASMainLayout();

        _detailsView = new JASDetailsView();
        _chargesView = new JASChargesView();

        _window.JASMainLayoutWrapper.AddChild(_mainLayout);
        _mainLayout.JASLogoutButton.OnPressed += _ => Logout();

        _mainLayout.JASDetailsViewButton.OnMouseEntered += _ => ChangeConsoleEntryText("jas -chl details");
        _mainLayout.JASChargesViewButton.OnMouseEntered += _ => ChangeConsoleEntryText("jas -chl charges");
        _mainLayout.JASWitnessViewButton.OnMouseEntered += _ => ChangeConsoleEntryText("jas -chl witness");
        _mainLayout.JASExportViewButton.OnMouseEntered += _ => ChangeConsoleEntryText("jas -chl export");

        _mainLayout.JASDetailsViewButton.OnPressed += _ => ShowTab(JASTabs.Details);
        _mainLayout.JASChargesViewButton.OnPressed += _ => ShowTab(JASTabs.Charges);
    }

    private void PopulateDetailsView()
    {
        if (_detailsView == null || _mainLayout == null)
            return;

        _detailsView.JASDetailsViewSaveButton.OnPressed += _ =>
            SendPredictedMessage(new JASDetailsSaveMsg(Rope.Collapse(_detailsView.JASDetailsViewInput.TextRope)));
        _mainLayout.JASMainLayoutViewsContainer.AddChild(_detailsView);
    }

    private void PopulateChragesView()
    {
        if (_chargesView == null || _mainLayout == null)
            return;
        if (!EntMan.TryGetComponent(Owner, out JASComponent? jasComp))
            return;

        _mainLayout.JASMainLayoutViewsContainer.AddChild(_chargesView);
    }

    private void ShowTab(JASTabs tab)
    {
        if (_window == null || _mainLayout == null)
            return;

        _mainLayout.JASMainLayoutViewsContainer.RemoveAllChildren();

        switch (tab)
        {
            case JASTabs.Details:
                ChangeConsoleEntryText("jas -chl details");
                PopulateDetailsView();
                break;
            case JASTabs.Charges:
                ChangeConsoleEntryText("jas -chl charges");
                PopulateChragesView();
                break;
        }

        SendPredictedMessage(new JASTabSelectedMsg(tab));
    }

    private void ChangeConsoleEntryText(string? text)
    {
        if (_mainLayout == null)
            return;

        _mainLayout.Cursor.Text = string.IsNullOrEmpty(text) ?  "█" : text;
    }

    private void Login()
    {
        if (_window == null)
            return;

        SendPredictedMessage(new JASPrivilegedIdInsertedMsg());

    }

    private void Logout()
    {
        if (_window == null)
            return;

        SendPredictedMessage(new JASPrivilegedIdEjectedMsg());
    }

}
