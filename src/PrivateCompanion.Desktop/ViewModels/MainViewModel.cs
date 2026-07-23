using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrivateCompanion.Application;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Desktop.ViewModels;

public partial class MainViewModel(
    CompanionApplicationService application,
    IMediaLibrary mediaLibrary,
    IProviderSecretStore providerSecretStore) : ViewModelBase
{
    private readonly ICompanionRepository _repository = application.Repository;
    private readonly IMediaLibrary _mediaLibrary = mediaLibrary;
    private CancellationTokenSource? _sendCancellation;
    private Guid _editingCharacterId;
    private Guid _editingUserPersonaId;
    private Guid _editingUniverseId;
    private ProviderProfile? _provider;
    private CharacterMediaPresentation? _mediaPresentation;
    private bool _isInitializing;
    private bool _isLoadingProviderFields;

    public ObservableCollection<UserPersonaListItem> UserPersonas { get; } = [];
    public ObservableCollection<UniverseListItem> Universes { get; } = [];
    public ObservableCollection<CharacterListItem> Characters { get; } = [];
    public ObservableCollection<ConversationListItem> Conversations { get; } = [];
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<MemoryListItem> Memories { get; } = [];
    public ObservableCollection<DiaryListItem> Diary { get; } = [];
    public ObservableCollection<GalleryItemViewModel> Gallery { get; } = [];
    public ObservableCollection<ProviderProfile> Providers { get; } = [];
    public ObservableCollection<string> DetectedProviderModels { get; } = [];
    public IReadOnlyList<ProviderPreset> RemoteProviderPresets { get; } = ProviderPresetCatalog.Online;
    public IReadOnlyList<MemoryKind> MemoryKinds { get; } = Enum.GetValues<MemoryKind>();
    public IReadOnlyList<ProviderKind> ProviderKinds { get; } = Enum.GetValues<ProviderKind>();
    public IReadOnlyList<ProviderLocation> ProviderLocations { get; } = Enum.GetValues<ProviderLocation>();
    public IReadOnlyList<SceneMode> SceneModes { get; } = Enum.GetValues<SceneMode>();

    [ObservableProperty] public partial string ActiveSection { get; set; } = "Inicio";
    [ObservableProperty] public partial CharacterListItem? SelectedCharacter { get; set; }
    [ObservableProperty] public partial UniverseListItem? SelectedUniverse { get; set; }
    [ObservableProperty] public partial UserPersonaListItem? SelectedUserPersona { get; set; }
    [ObservableProperty] public partial ConversationListItem? SelectedConversation { get; set; }
    [ObservableProperty] public partial MemoryListItem? SelectedMemory { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Preparando tu espacio privado…";
    [ObservableProperty] public partial string ProviderStatus { get; set; } = "Motor sin comprobar";
    [ObservableProperty] public partial string ChatInput { get; set; } = string.Empty;

    [ObservableProperty] public partial string UniverseName { get; set; } = string.Empty;
    [ObservableProperty] public partial string UniverseSummary { get; set; } = string.Empty;
    [ObservableProperty] public partial string UniverseCanon { get; set; } = string.Empty;
    [ObservableProperty] public partial string UniverseRules { get; set; } = string.Empty;
    [ObservableProperty] public partial string UniverseTone { get; set; } = string.Empty;
    [ObservableProperty] public partial string UniverseSource { get; set; } = string.Empty;

    [ObservableProperty] public partial string UserName { get; set; } = string.Empty;
    [ObservableProperty] public partial int UserAge { get; set; } = 25;
    [ObservableProperty] public partial string UserPronouns { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserAppearance { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserAbout { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserProfession { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserLikes { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserCommunicationStyle { get; set; } = string.Empty;
    [ObservableProperty] public partial string UserPortraitRelativePath { get; set; } = string.Empty;
    [ObservableProperty] public partial Bitmap? UserPortraitPreview { get; set; }
    [ObservableProperty] public partial string IdentityStatusText { get; set; } = "Los cambios quedan en este equipo cuando presionás «Guardar y usar».";

    [ObservableProperty] public partial string CharacterName { get; set; } = string.Empty;
    [ObservableProperty] public partial int CharacterAge { get; set; } = 25;
    [ObservableProperty] public partial string Appearance { get; set; } = string.Empty;
    [ObservableProperty] public partial string Backstory { get; set; } = string.Empty;
    [ObservableProperty] public partial string PersonalityNotes { get; set; } = string.Empty;
    [ObservableProperty] public partial string Profession { get; set; } = string.Empty;
    [ObservableProperty] public partial string Likes { get; set; } = string.Empty;
    [ObservableProperty] public partial string SpeechStyle { get; set; } = string.Empty;
    [ObservableProperty] public partial string Goals { get; set; } = string.Empty;
    [ObservableProperty] public partial string AffectiveStyle { get; set; } = string.Empty;
    [ObservableProperty] public partial string RomanticStyle { get; set; } = string.Empty;
    [ObservableProperty] public partial string HumorStyle { get; set; } = string.Empty;
    [ObservableProperty] public partial string CharacterLanguage { get; set; } = "Español";
    [ObservableProperty] public partial double Romanticism { get; set; } = 0.75;
    [ObservableProperty] public partial double Extroversion { get; set; } = 0.55;
    [ObservableProperty] public partial double Humor { get; set; } = 0.60;
    [ObservableProperty] public partial double Calm { get; set; } = 0.65;
    [ObservableProperty] public partial double Confidence { get; set; } = 0.70;
    [ObservableProperty] public partial double Affection { get; set; } = 0.80;
    [ObservableProperty] public partial double Independence { get; set; } = 0.55;
    [ObservableProperty] public partial double Initiative { get; set; } = 0.70;
    [ObservableProperty] public partial double Formality { get; set; } = 0.20;
    [ObservableProperty] public partial double EmotionalIntensity { get; set; } = 0.75;

    [ObservableProperty] public partial string SceneTitle { get; set; } = "Encuentro cotidiano";
    [ObservableProperty] public partial string ScenePremise { get; set; } = string.Empty;
    [ObservableProperty] public partial SceneMode SelectedSceneMode { get; set; } = SceneMode.Canonical;
    [ObservableProperty] public partial string ScenePlace { get; set; } = string.Empty;
    [ObservableProperty] public partial string SceneTime { get; set; } = string.Empty;
    [ObservableProperty] public partial string SceneAtmosphere { get; set; } = string.Empty;
    [ObservableProperty] public partial string SceneParticipants { get; set; } = string.Empty;
    [ObservableProperty] public partial string SceneIntent { get; set; } = string.Empty;
    [ObservableProperty] public partial string SceneKnownFacts { get; set; } = string.Empty;
    [ObservableProperty] public partial string SceneBoundaries { get; set; } = string.Empty;

    [ObservableProperty] public partial double RelationshipTrust { get; set; }
    [ObservableProperty] public partial double RelationshipCloseness { get; set; }
    [ObservableProperty] public partial double RelationshipAffection { get; set; }
    [ObservableProperty] public partial double RelationshipIntimacy { get; set; }
    [ObservableProperty] public partial string RelationshipMood { get; set; } = string.Empty;

    [ObservableProperty] public partial string NewMemoryTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewMemoryContent { get; set; } = string.Empty;
    [ObservableProperty] public partial MemoryKind SelectedMemoryKind { get; set; } = MemoryKind.Personal;
    [ObservableProperty] public partial double NewMemoryImportance { get; set; } = 0.65;

    [ObservableProperty] public partial ProviderProfile? SelectedProviderProfile { get; set; }
    [ObservableProperty] public partial ProviderKind SelectedProviderKind { get; set; } = ProviderKind.LlamaCpp;
    [ObservableProperty] public partial ProviderLocation SelectedProviderLocation { get; set; } = ProviderLocation.Local;
    [ObservableProperty] public partial string ProviderName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProviderBaseUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProviderModel { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProviderLaunchCommand { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ProviderAutoStart { get; set; }
    [ObservableProperty] public partial string ProviderApiToken { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasStoredProviderToken { get; set; }
    [ObservableProperty] public partial ProviderPreset? SelectedRemoteProviderPreset { get; set; }
    [ObservableProperty] public partial string? SelectedDetectedProviderModel { get; set; }
    [ObservableProperty] public partial string ProviderSetupHelp { get; set; } = "Elegí una opción para completar la conexión sin memorizar direcciones técnicas.";
    [ObservableProperty] public partial string ProviderEditorTitle { get; set; } = "Motor local";
    [ObservableProperty] public partial string ProviderEditorDescription { get; set; } = "Tu motor actual funciona en este equipo.";
    [ObservableProperty] public partial string ActiveProviderText { get; set; } = "Cargando motor activo…";
    [ObservableProperty] public partial double ProviderTemperature { get; set; } = 0.85;
    [ObservableProperty] public partial int ProviderMaxTokens { get; set; } = 512;

    [ObservableProperty] public partial GalleryItemViewModel? SelectedGalleryItem { get; set; }
    [ObservableProperty] public partial GalleryItemViewModel? AlbumItem { get; set; }
    [ObservableProperty] public partial Bitmap? CharacterPortraitPreview { get; set; }
    [ObservableProperty] public partial Bitmap? ChatBackgroundPreview { get; set; }
    [ObservableProperty] public partial double AlbumZoom { get; set; } = 1.0;
    [ObservableProperty] public partial double AlbumFocusX { get; set; } = 0.5;
    [ObservableProperty] public partial double AlbumFocusY { get; set; } = 0.5;
    [ObservableProperty] public partial double PortraitZoom { get; set; } = 1.0;
    [ObservableProperty] public partial double PortraitFocusX { get; set; } = 0.5;
    [ObservableProperty] public partial double PortraitFocusY { get; set; } = 0.5;
    [ObservableProperty] public partial double BackgroundZoom { get; set; } = 1.0;
    [ObservableProperty] public partial double BackgroundFocusX { get; set; } = 0.5;
    [ObservableProperty] public partial double BackgroundFocusY { get; set; } = 0.5;

    public bool IsHome => ActiveSection == "Inicio";
    public bool IsChat => ActiveSection == "Chat";
    public bool IsUniverse => ActiveSection == "Universo";
    public bool IsProfile => ActiveSection == "Perfil";
    public bool IsMemory => ActiveSection == "Memoria";
    public bool IsGallery => ActiveSection == "Galería";
    public bool IsUsers => ActiveSection == "Usuarios";
    public bool IsSettings => ActiveSection == "Configuración";
    public bool IsLocalProvider => SelectedProviderLocation == ProviderLocation.Local;
    public bool IsRemoteProvider => SelectedProviderLocation == ProviderLocation.Remote;
    public bool HasDetectedProviderModels => DetectedProviderModels.Count > 0;
    public bool HasProviderHelpLink => !string.IsNullOrWhiteSpace(SelectedRemoteProviderPreset?.HelpUrl);
    public bool IsCustomRemoteProvider => IsRemoteProvider && SelectedRemoteProviderPreset?.Id == "custom";
    public bool IsHostedRemoteProvider => IsRemoteProvider && !IsCustomRemoteProvider;
    public string ProviderPrivacyText => IsLocalProvider
        ? "Modo local: prompts, respuestas y datos permanecen en este equipo."
        : "Modo online: los chats se guardan localmente, pero el contexto necesario para responder se envía al servidor remoto.";
    public bool HasGalleryItems => Gallery.Count > 0;
    public bool HasSelectedCharacter => SelectedCharacter is not null;
    public bool HasSelectedConversation => SelectedConversation is not null;
    public bool CanStartChat => SelectedCharacter is not null && SelectedUserPersona is not null;
    public bool HasPortrait => CharacterPortraitPreview is not null;
    public bool HasUserPortrait => UserPortraitPreview is not null;
    public bool HasChatBackground => ChatBackgroundPreview is not null;
    public bool HasAlbumItem => AlbumItem is not null;
    public RelativePoint AlbumTransformOrigin => new(AlbumFocusX, AlbumFocusY, RelativeUnit.Relative);
    public RelativePoint PortraitTransformOrigin => new(PortraitFocusX, PortraitFocusY, RelativeUnit.Relative);
    public RelativePoint BackgroundTransformOrigin => new(BackgroundFocusX, BackgroundFocusY, RelativeUnit.Relative);
    public string SelectedCharacterSubtitle => SelectedCharacter is null ? "Sin personaje" : $"{SelectedCharacter.Name}, {SelectedCharacter.Age}";
    public string SelectedCharacterDetail => SelectedCharacter is null
        ? "Elegí un personaje para cargar su perfil y sus conversaciones."
        : $"Revisión {SelectedCharacter.Revision} · editado {SelectedCharacter.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}";
    public string ActiveUserSubtitle => SelectedUserPersona is null ? "Sin identidad" : $"Conversando como {SelectedUserPersona.Name}";

    public async Task InitializeAsync()
    {
        _isInitializing = true;
        await _repository.InitializeAsync();
        await LoadUserPersonasAsync();
        await LoadUniversesAsync();
        await LoadCharactersAsync(preserveNoSelection: true);
        await LoadProvidersAsync();
        _isInitializing = false;
        if (SelectedCharacter is not null && SelectedUserPersona is not null)
            await LoadCharacterWorkspaceAsync(SelectedCharacter.Id);
        StatusText = "Elegí una identidad y un personaje para empezar. Todo permanece en este equipo.";
    }

    partial void OnActiveSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsHome)); OnPropertyChanged(nameof(IsChat)); OnPropertyChanged(nameof(IsUniverse)); OnPropertyChanged(nameof(IsProfile)); OnPropertyChanged(nameof(IsMemory));
        OnPropertyChanged(nameof(IsGallery)); OnPropertyChanged(nameof(IsUsers)); OnPropertyChanged(nameof(IsSettings));
    }

    partial void OnSelectedCharacterChanged(CharacterListItem? value)
    {
        OnPropertyChanged(nameof(SelectedCharacterSubtitle));
        OnPropertyChanged(nameof(SelectedCharacterDetail));
        OnPropertyChanged(nameof(HasSelectedCharacter));
        OnPropertyChanged(nameof(CanStartChat));
        if (!_isInitializing && value is not null && SelectedUserPersona is not null) _ = LoadCharacterWorkspaceAsync(value.Id);
    }

    partial void OnSelectedUniverseChanged(UniverseListItem? value)
    {
        if (value is not null) _ = LoadUniverseAsync(value.Id);
    }

    partial void OnSelectedUserPersonaChanged(UserPersonaListItem? value)
    {
        OnPropertyChanged(nameof(ActiveUserSubtitle));
        OnPropertyChanged(nameof(CanStartChat));
        if (value is null) return;
        LoadUserPersonaFields(value);
        if (!_isInitializing) _ = ActivateUserPersonaAsync(value.Id);
    }

    partial void OnSelectedConversationChanged(ConversationListItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedConversation));
        if (value is not null) _ = LoadConversationAsync(value.Id);
    }

    partial void OnSelectedProviderProfileChanged(ProviderProfile? value)
    {
        if (value is not null) _ = LoadProviderFieldsAsync(value);
    }

    partial void OnSelectedProviderLocationChanged(ProviderLocation value)
    {
        if (value == ProviderLocation.Remote) ProviderAutoStart = false;
        OnPropertyChanged(nameof(IsLocalProvider));
        OnPropertyChanged(nameof(IsRemoteProvider));
        OnPropertyChanged(nameof(IsCustomRemoteProvider));
        OnPropertyChanged(nameof(IsHostedRemoteProvider));
        OnPropertyChanged(nameof(ProviderPrivacyText));
    }

    partial void OnSelectedRemoteProviderPresetChanged(ProviderPreset? value)
    {
        OnPropertyChanged(nameof(HasProviderHelpLink));
        OnPropertyChanged(nameof(IsCustomRemoteProvider));
        OnPropertyChanged(nameof(IsHostedRemoteProvider));
        if (value is null) return;
        ProviderSetupHelp = value.Description;
        if (_isLoadingProviderFields) return;

        SelectedProviderLocation = ProviderLocation.Remote;
        SelectedProviderKind = ProviderKind.OpenAICompatible;
        ProviderName = value.ProfileName;
        ProviderBaseUrl = value.BaseUrl;
        ProviderModel = string.Empty;
        ProviderEditorTitle = "Nueva conexión online";
        ProviderEditorDescription = value.Id == "custom"
            ? "Conectá un servidor propio o independiente mediante una dirección HTTPS."
            : $"Conectá {value.ProfileName} en tres pasos. Los datos narrativos continúan guardados en tu PC.";
        DetectedProviderModels.Clear();
        SelectedDetectedProviderModel = null;
        OnPropertyChanged(nameof(HasDetectedProviderModels));
        StatusText = value.Id == "custom"
            ? "Ingresá la dirección HTTPS de tu servidor y, si corresponde, su clave."
            : $"Preset «{value.Label}» preparado. Falta tu clave y elegir un modelo.";
    }

    partial void OnSelectedDetectedProviderModelChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) ProviderModel = value;
    }

    partial void OnSelectedGalleryItemChanged(GalleryItemViewModel? value)
    {
        if (value is not null) AlbumItem = value;
    }

    partial void OnAlbumItemChanged(GalleryItemViewModel? value)
    {
        ResetAlbumView();
        OnPropertyChanged(nameof(HasAlbumItem));
    }
    partial void OnCharacterPortraitPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasPortrait));
    partial void OnUserPortraitPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasUserPortrait));
    partial void OnChatBackgroundPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasChatBackground));
    partial void OnAlbumFocusXChanged(double value) => OnPropertyChanged(nameof(AlbumTransformOrigin));
    partial void OnAlbumFocusYChanged(double value) => OnPropertyChanged(nameof(AlbumTransformOrigin));
    partial void OnPortraitFocusXChanged(double value) => OnPropertyChanged(nameof(PortraitTransformOrigin));
    partial void OnPortraitFocusYChanged(double value) => OnPropertyChanged(nameof(PortraitTransformOrigin));
    partial void OnBackgroundFocusXChanged(double value) => OnPropertyChanged(nameof(BackgroundTransformOrigin));
    partial void OnBackgroundFocusYChanged(double value) => OnPropertyChanged(nameof(BackgroundTransformOrigin));

    [RelayCommand]
    private void Navigate(string section)
    {
        if (section == "Chat" && !CanStartChat)
        {
            ActiveSection = "Inicio";
            StatusText = "Elegí una identidad y un personaje antes de abrir la conversación.";
            return;
        }
        ActiveSection = section;
    }

    [RelayCommand]
    private void OpenSelectedConversation()
    {
        if (!CanStartChat || SelectedConversation is null)
        {
            StatusText = "Elegí un personaje y una conversación para continuar.";
            return;
        }
        ActiveSection = "Chat";
        StatusText = $"Conversación cargada con {SelectedCharacter!.Name}.";
    }

    [RelayCommand]
    private void NewUniverse()
    {
        _editingUniverseId = Guid.NewGuid();
        SelectedUniverse = null;
        UniverseName = "Universo nuevo";
        UniverseSummary = UniverseCanon = UniverseRules = UniverseTone = string.Empty;
        UniverseSource = "Creado en Tinta Viva";
        ActiveSection = "Universo";
        StatusText = "Definí el mundo compartido por tus personajes e historias.";
    }

    [RelayCommand]
    private async Task SaveUniverseAsync()
    {
        try
        {
            var universe = new StoryUniverse
            {
                Id = _editingUniverseId == Guid.Empty ? Guid.NewGuid() : _editingUniverseId,
                Name = UniverseName.Trim(),
                Summary = UniverseSummary.Trim(),
                Canon = UniverseCanon.Trim(),
                Rules = UniverseRules.Trim(),
                Tone = UniverseTone.Trim(),
                Source = UniverseSource.Trim()
            };
            await _repository.SaveUniverseAsync(universe);
            await LoadUniversesAsync(universe.Id);
            StatusText = "Universo guardado. Sus reglas y canon ya forman parte del contexto local.";
        }
        catch (ValidationException ex) { StatusText = ex.Message; }
        catch (Exception ex) { StatusText = $"No pude guardar el universo: {ex.Message}"; }
    }

    [RelayCommand]
    private void NewUserPersona()
    {
        _editingUserPersonaId = Guid.NewGuid();
        SelectedUserPersona = null;
        UserName = string.Empty;
        UserAge = 25;
        UserPronouns = string.Empty;
        UserAppearance = string.Empty;
        UserAbout = string.Empty;
        UserProfession = string.Empty;
        UserLikes = string.Empty;
        UserCommunicationStyle = "Español rioplatense natural, directo y sin exagerar modismos.";
        UserPortraitRelativePath = string.Empty;
        SetUserPortraitPreview(null);
        ActiveSection = "Usuarios";
        StatusText = "Creá una identidad. Su memoria y sus vínculos quedarán separados.";
        IdentityStatusText = "Identidad nueva: completá al menos el nombre y la edad, elegí un retrato si querés y guardá.";
    }

    [RelayCommand]
    private async Task SaveUserPersonaAsync()
    {
        try
        {
            var userPersona = new UserPersona
            {
                Id = _editingUserPersonaId == Guid.Empty ? Guid.NewGuid() : _editingUserPersonaId,
                Name = UserName.Trim(),
                Age = UserAge,
                Pronouns = UserPronouns.Trim(),
                Appearance = UserAppearance.Trim(),
                About = UserAbout.Trim(),
                Profession = UserProfession.Trim(),
                Likes = UserLikes.Trim(),
                CommunicationStyle = UserCommunicationStyle.Trim(),
                PortraitRelativePath = UserPortraitRelativePath,
                IsActive = true
            };
            await _repository.SaveUserPersonaAsync(userPersona);
            await _repository.SetActiveUserPersonaAsync(userPersona.Id);
            await LoadUserPersonasAsync(userPersona.Id);
            if (SelectedCharacter is not null) await LoadCharacterWorkspaceAsync(SelectedCharacter.Id);
            var savedAt = DateTime.Now.ToString("HH:mm");
            StatusText = "Identidad guardada. Sus conversaciones y recuerdos quedan aislados de los demás perfiles.";
            IdentityStatusText = $"✓ Guardado a las {savedAt}. «{userPersona.Name}» está activa y su retrato quedó asociado solo a esta identidad.";
        }
        catch (ValidationException ex) { StatusText = IdentityStatusText = ex.Message; }
        catch (Exception ex) { StatusText = IdentityStatusText = $"No pude guardar la identidad: {ex.Message}"; }
    }

    public async Task ImportUserPortraitAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || IsBusy) return;
        IsBusy = true;
        try
        {
            UserPortraitRelativePath = await _mediaLibrary.ImportUserPortraitAsync(sourcePath);
            SetUserPortraitPreview(_mediaLibrary.GetAbsolutePath(UserPortraitRelativePath));
            StatusText = "Retrato preparado para tu identidad.";
            IdentityStatusText = "Retrato preparado. Presioná «Guardar y usar» para asociarlo a esta identidad.";
        }
        catch (Exception ex)
        {
            StatusText = IdentityStatusText = $"No pude preparar el retrato: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearUserPortrait()
    {
        UserPortraitRelativePath = string.Empty;
        SetUserPortraitPreview(null);
        StatusText = "Retrato quitado del formulario.";
        IdentityStatusText = "El retrato se quitará de esta identidad cuando presiones «Guardar y usar».";
    }

    [RelayCommand]
    private void NewCharacter()
    {
        _editingCharacterId = Guid.NewGuid();
        CharacterName = ""; CharacterAge = 25; Appearance = ""; Backstory = ""; PersonalityNotes = "";
        Profession = ""; Likes = ""; SpeechStyle = "Rioplatense natural y actual, con voseo sutil; sin forzar lunfardo ni muletillas."; Goals = ""; AffectiveStyle = ""; RomanticStyle = "";
        HumorStyle = ""; CharacterLanguage = "Español rioplatense";
        Romanticism = 0.65; Extroversion = 0.5; Humor = 0.5; Calm = 0.5; Confidence = 0.6;
        Affection = 0.7; Independence = 0.5; Initiative = 0.6; Formality = 0.3; EmotionalIntensity = 0.65;
        ActiveSection = "Perfil";
        StatusText = "Definí un personaje nuevo.";
    }

    [RelayCommand]
    private async Task SaveCharacterAsync()
    {
        try
        {
            var character = new Character
            {
                Id = _editingCharacterId == Guid.Empty ? Guid.NewGuid() : _editingCharacterId,
                UniverseId = SelectedUniverse?.Id,
                Name = CharacterName.Trim(), Age = CharacterAge, Appearance = Appearance, Backstory = Backstory,
                PersonalityNotes = PersonalityNotes, Profession = Profession, Likes = Likes, SpeechStyle = SpeechStyle,
                Goals = Goals, AffectiveStyle = AffectiveStyle, RomanticStyle = RomanticStyle, HumorStyle = HumorStyle,
                Language = CharacterLanguage, Romanticism = Romanticism, Extroversion = Extroversion, Humor = Humor,
                Calm = Calm, Confidence = Confidence, Affection = Affection, Independence = Independence,
                Initiative = Initiative, Formality = Formality, EmotionalIntensity = EmotionalIntensity
            };
            await _repository.SaveCharacterAsync(character);
            await LoadCharactersAsync(character.Id);
            StatusText = "Perfil guardado con una nueva revisión.";
        }
        catch (ValidationException ex) { StatusText = ex.Message; }
        catch (Exception ex) { StatusText = $"No pude guardar el perfil: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        if (SelectedCharacter is null || SelectedUserPersona is null) return;
        var conversation = await application.CreateConversationAsync(SelectedCharacter.Id, SelectedUserPersona.Id, mode: SelectedSceneMode);
        await LoadConversationsAsync(SelectedCharacter.Id, SelectedUserPersona.Id, conversation.Id);
        ActiveSection = "Chat";
    }

    [RelayCommand]
    private async Task SaveSceneAsync()
    {
        if (SelectedConversation is null) return;
        var conversation = await _repository.GetConversationAsync(SelectedConversation.Id);
        if (conversation is null) return;
        conversation.SceneTitle = string.IsNullOrWhiteSpace(SceneTitle) ? "Encuentro cotidiano" : SceneTitle.Trim();
        conversation.ScenePremise = ScenePremise.Trim();
        conversation.SceneMode = SelectedSceneMode;
        conversation.ScenePlace = ScenePlace.Trim();
        conversation.SceneTime = SceneTime.Trim();
        conversation.SceneAtmosphere = SceneAtmosphere.Trim();
        conversation.SceneParticipants = SceneParticipants.Trim();
        conversation.SceneIntent = SceneIntent.Trim();
        conversation.SceneKnownFacts = SceneKnownFacts.Trim();
        conversation.SceneBoundaries = SceneBoundaries.Trim();
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveConversationAsync(conversation);
        StatusText = conversation.SceneMode == SceneMode.Canonical ? "Escena canónica guardada." : "Escena sandbox guardada; no contaminará la memoria canónica.";
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (SelectedCharacter is null || SelectedUserPersona is null || string.IsNullOrWhiteSpace(ChatInput) || IsBusy) return;
        if (SelectedConversation is null) await NewConversationAsync();
        if (SelectedConversation is null) return;

        var text = ChatInput.Trim();
        ChatInput = string.Empty;
        Messages.Add(new ChatMessageViewModel(SelectedUserPersona.Name, text, true));
        var assistant = new ChatMessageViewModel(SelectedCharacter.Name, "", false);
        Messages.Add(assistant);
        IsBusy = true;
        ProviderStatus = "Generando localmente…";
        _sendCancellation = new CancellationTokenSource();
        try
        {
            await foreach (var chunk in application.SendAsync(SelectedCharacter.Id, SelectedConversation.Id, text, _sendCancellation.Token))
                assistant.Content += chunk;
            ProviderStatus = "Motor local conectado";
            StatusText = "Respuesta guardada localmente.";
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrWhiteSpace(assistant.Content)) assistant.Content = "[Generación cancelada]";
            ProviderStatus = "Generación cancelada";
        }
        catch (Exception ex)
        {
            assistant.Content = $"No pude generar la respuesta. {ex.Message}";
            ProviderStatus = "Motor local desconectado";
            StatusText = "Revisá Configuración si el motor no pudo iniciarse.";
        }
        finally
        {
            IsBusy = false;
            _sendCancellation?.Dispose(); _sendCancellation = null;
            await LoadConversationsAsync(SelectedCharacter.Id, SelectedUserPersona.Id, SelectedConversation.Id);
        }
    }

    [RelayCommand] private void CancelGeneration() => _sendCancellation?.Cancel();

    [RelayCommand]
    private async Task CloseSessionAsync()
    {
        if (SelectedConversation is null || IsBusy) return;
        IsBusy = true;
        StatusText = "Creando diario y consolidando memoria…";
        try
        {
            await application.CloseSessionAsync(SelectedConversation.Id);
            if (SelectedCharacter is not null && SelectedUserPersona is not null) await LoadCharacterWorkspaceAsync(SelectedCharacter.Id, keepConversation: true);
            StatusText = "Sesión cerrada: diario, memoria y relación actualizados.";
        }
        catch (Exception ex) { StatusText = $"No pude cerrar la sesión: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddMemoryAsync()
    {
        if (SelectedCharacter is null || SelectedUserPersona is null || string.IsNullOrWhiteSpace(NewMemoryContent)) return;
        await _repository.SaveMemoryAsync(new Memory
        {
            CharacterId = SelectedCharacter.Id,
            UserPersonaId = SelectedUserPersona.Id,
            Kind = SelectedMemoryKind,
            Title = string.IsNullOrWhiteSpace(NewMemoryTitle) ? "Recuerdo manual" : NewMemoryTitle.Trim(),
            Content = NewMemoryContent.Trim(),
            Importance = NewMemoryImportance,
            Confidence = 1,
            IsCanonical = true
        });
        NewMemoryTitle = ""; NewMemoryContent = "";
        await LoadMemoriesAsync(SelectedCharacter.Id, SelectedUserPersona.Id);
        StatusText = "Recuerdo agregado con origen manual.";
    }

    [RelayCommand]
    private async Task DeleteSelectedMemoryAsync()
    {
        if (SelectedMemory is null || SelectedCharacter is null || SelectedUserPersona is null) return;
        await _repository.DeleteMemoryAsync(SelectedMemory.Id);
        await LoadMemoriesAsync(SelectedCharacter.Id, SelectedUserPersona.Id);
        StatusText = "Recuerdo eliminado.";
    }

    [RelayCommand]
    private async Task SaveProviderAsync()
    {
        try
        {
            if (!TryBuildProviderDraft(out var provider)) return;
            await PersistProviderAsync(provider);
        }
        catch (Exception ex)
        {
            ProviderStatus = ex.Message;
        }
    }

    [RelayCommand]
    private void NewLocalProvider()
    {
        _provider = new ProviderProfile
        {
            Name = "Motor local",
            Kind = ProviderKind.LMStudio,
            Location = ProviderLocation.Local,
            BaseUrl = "http://127.0.0.1:1234/v1"
        };
        LoadProviderFields(_provider, false);
        ProviderEditorTitle = "Nuevo motor local";
        ProviderEditorDescription = "Para LM Studio, Ollama o llama.cpp ejecutándose en esta computadora.";
        StatusText = "Configurá el nuevo motor local y presioná «Guardar y usar».";
    }

    [RelayCommand]
    private void NewRemoteProvider()
    {
        var preset = ProviderPresetCatalog.Online.First(x => x.Id == "venice");
        _provider = new ProviderProfile
        {
            Name = preset.ProfileName,
            Kind = ProviderKind.OpenAICompatible,
            Location = ProviderLocation.Remote,
            BaseUrl = preset.BaseUrl,
            AutoStart = false
        };
        LoadProviderFields(_provider, false);
        SelectedRemoteProviderPreset = preset;
        ProviderEditorTitle = "Nueva conexión online";
        ProviderEditorDescription = "Elegí un servicio y seguí los pasos. Los campos técnicos quedan en «Configuración avanzada».";
        StatusText = "Elegí una opción guiada. Ningún servicio se conecta hasta que agregues sus datos y lo compruebes.";
    }

    [RelayCommand]
    private async Task DeleteProviderTokenAsync()
    {
        if (_provider is null) return;
        await providerSecretStore.DeleteAsync(_provider.Id);
        ProviderApiToken = string.Empty;
        HasStoredProviderToken = false;
        StatusText = "Token protegido eliminado. El perfil del motor sigue guardado.";
    }

    [RelayCommand]
    private async Task TestProviderAsync()
    {
        if (!TryBuildProviderDraft(out var provider)) return;
        ProviderStatus = "Comprobando sin cambiar el motor activo…";
        try
        {
            var probe = await ProbeDraftAsync(provider, IsLocalProvider);
            if (!probe.IsAvailable)
            {
                ProviderStatus = $"{probe.Message} Tu motor anterior continúa activo.";
                return;
            }

            UpdateDetectedModels(probe.Models);
            if (string.IsNullOrWhiteSpace(ProviderModel) && probe.Models.Count > 0)
                SelectedDetectedProviderModel = probe.Models[0];
            provider.Model = ProviderModel.Trim();
            await PersistProviderAsync(provider);
            ProviderStatus = $"{probe.Message} Perfil guardado y activado.";
        }
        catch (Exception ex) { ProviderStatus = ex.Message; }
    }

    [RelayCommand]
    private async Task DetectProviderModelsAsync()
    {
        if (!TryBuildProviderDraft(out var provider)) return;
        ProviderStatus = "Buscando modelos sin cambiar el motor activo…";
        try
        {
            var probe = await ProbeDraftAsync(provider, IsLocalProvider);
            if (!probe.IsAvailable)
            {
                ProviderStatus = $"{probe.Message} No se modificó tu motor activo.";
                return;
            }

            UpdateDetectedModels(probe.Models);
            ProviderStatus = probe.Models.Count switch
            {
                0 => "El servidor respondió, pero no informó modelos. Podés escribir el identificador manualmente.",
                1 => "Encontré un modelo y lo seleccioné. Revisalo y presioná «Guardar y comprobar».",
                _ => $"Encontré {probe.Models.Count} modelos. Elegí uno de la lista."
            };
        }
        catch (Exception ex) { ProviderStatus = ex.Message; }
    }

    [RelayCommand]
    private void OpenProviderHelp()
    {
        var url = SelectedRemoteProviderPreset?.HelpUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ProviderStatus = $"No pude abrir la página: {ex.Message}";
        }
    }

    public async Task ImportImagesAsync(IReadOnlyList<string> sourcePaths)
    {
        if (SelectedCharacter is null || sourcePaths.Count == 0) return;
        IsBusy = true;
        StatusText = "Copiando imágenes al almacenamiento privado…";
        try
        {
            var result = await _mediaLibrary.ImportAsync(SelectedCharacter.Id, sourcePaths);
            await LoadGalleryAsync(SelectedCharacter.Id);
            ActiveSection = "Galería";
            var summary = $"Importadas: {result.Imported}. Duplicadas omitidas: {result.Skipped}.";
            StatusText = result.Errors.Count == 0
                ? summary
                : $"{summary} No se pudieron agregar {result.Errors.Count}: {string.Join(" | ", result.Errors.Take(2))}";
        }
        catch (Exception ex)
        {
            StatusText = $"No pude importar las imágenes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PreviousAlbumImage()
    {
        if (Gallery.Count == 0) return;
        var index = AlbumItem is null ? 0 : Gallery.IndexOf(AlbumItem);
        AlbumItem = Gallery[(index - 1 + Gallery.Count) % Gallery.Count];
        SelectedGalleryItem = AlbumItem;
    }

    [RelayCommand]
    private void NextAlbumImage()
    {
        if (Gallery.Count == 0) return;
        var index = AlbumItem is null ? -1 : Gallery.IndexOf(AlbumItem);
        AlbumItem = Gallery[(index + 1) % Gallery.Count];
        SelectedGalleryItem = AlbumItem;
    }

    [RelayCommand]
    private void ResetAlbumView()
    {
        AlbumZoom = 1;
        AlbumFocusX = AlbumFocusY = 0.5;
    }

    [RelayCommand]
    private async Task UseAlbumAsPortraitAsync()
    {
        if (SelectedCharacter is null || AlbumItem is null) return;
        _mediaPresentation ??= new CharacterMediaPresentation { CharacterId = SelectedCharacter.Id };
        _mediaPresentation.PortraitMediaAssetId = AlbumItem.Id;
        PortraitZoom = _mediaPresentation.PortraitZoom = 1;
        PortraitFocusX = _mediaPresentation.PortraitFocusX = 0.5;
        PortraitFocusY = _mediaPresentation.PortraitFocusY = 0.5;
        await _repository.SaveMediaPresentationAsync(_mediaPresentation);
        RefreshPresentationPreviews();
        StatusText = $"{AlbumItem.Name} ahora es el retrato de {SelectedCharacter.Name}.";
    }

    [RelayCommand]
    private async Task UseAlbumAsBackgroundAsync()
    {
        if (SelectedCharacter is null || AlbumItem is null) return;
        _mediaPresentation ??= new CharacterMediaPresentation { CharacterId = SelectedCharacter.Id };
        _mediaPresentation.BackgroundMediaAssetId = AlbumItem.Id;
        _mediaPresentation.BackgroundEnabled = true;
        BackgroundZoom = _mediaPresentation.BackgroundZoom = 1;
        BackgroundFocusX = _mediaPresentation.BackgroundFocusX = 0.5;
        BackgroundFocusY = _mediaPresentation.BackgroundFocusY = 0.5;
        await _repository.SaveMediaPresentationAsync(_mediaPresentation);
        RefreshPresentationPreviews();
        StatusText = $"{AlbumItem.Name} quedó como fondo de la conversación.";
    }

    [RelayCommand]
    private async Task ClearChatBackgroundAsync()
    {
        if (SelectedCharacter is null || _mediaPresentation is null) return;
        _mediaPresentation.BackgroundMediaAssetId = null;
        await _repository.SaveMediaPresentationAsync(_mediaPresentation);
        RefreshPresentationPreviews();
        StatusText = "Fondo de conversación quitado. La imagen sigue guardada en el álbum.";
    }

    [RelayCommand]
    private async Task SaveMediaFramingAsync()
    {
        if (_mediaPresentation is null) return;
        _mediaPresentation.PortraitZoom = PortraitZoom;
        _mediaPresentation.PortraitFocusX = PortraitFocusX;
        _mediaPresentation.PortraitFocusY = PortraitFocusY;
        _mediaPresentation.BackgroundZoom = BackgroundZoom;
        _mediaPresentation.BackgroundFocusX = BackgroundFocusX;
        _mediaPresentation.BackgroundFocusY = BackgroundFocusY;
        await _repository.SaveMediaPresentationAsync(_mediaPresentation);
        StatusText = "Encuadre de retrato y fondo guardado sin modificar la imagen original.";
    }

    [RelayCommand]
    private async Task ResetMediaFramingAsync()
    {
        PortraitZoom = BackgroundZoom = 1;
        PortraitFocusX = PortraitFocusY = BackgroundFocusX = BackgroundFocusY = 0.5;
        await SaveMediaFramingAsync();
        StatusText = "Encuadre centrado y restablecido.";
    }

    private async Task LoadUserPersonasAsync(Guid? selectId = null)
    {
        var users = await _repository.GetUserPersonasAsync();
        var current = selectId ?? users.FirstOrDefault(x => x.IsActive)?.Id ?? SelectedUserPersona?.Id;
        var wasInitializing = _isInitializing;
        _isInitializing = true;
        UserPersonas.Clear();
        foreach (var user in users)
            UserPersonas.Add(new UserPersonaListItem(user.Id, user.Name, user.Age, user.Pronouns, user.Appearance,
                user.About, user.Profession, user.Likes, user.CommunicationStyle, user.PortraitRelativePath, user.IsActive));
        SelectedUserPersona = UserPersonas.FirstOrDefault(x => x.Id == current) ?? UserPersonas.FirstOrDefault();
        if (SelectedUserPersona is not null) LoadUserPersonaFields(SelectedUserPersona);
        _isInitializing = wasInitializing;
    }

    private void LoadUserPersonaFields(UserPersonaListItem user)
    {
        _editingUserPersonaId = user.Id;
        UserName = user.Name;
        UserAge = user.Age;
        UserPronouns = user.Pronouns;
        UserAppearance = user.Appearance;
        UserAbout = user.About;
        UserProfession = user.Profession;
        UserLikes = user.Likes;
        UserCommunicationStyle = user.CommunicationStyle;
        UserPortraitRelativePath = user.PortraitRelativePath;
        SetUserPortraitPreview(string.IsNullOrWhiteSpace(user.PortraitRelativePath)
            ? null
            : _mediaLibrary.GetAbsolutePath(user.PortraitRelativePath));
        IdentityStatusText = $"Identidad cargada: «{user.Name}». Los cambios se aplican con «Guardar y usar».";
    }

    private void SetUserPortraitPreview(string? absolutePath)
    {
        var previous = UserPortraitPreview;
        UserPortraitPreview = DecodeImage(absolutePath, 420);
        previous?.Dispose();
    }

    private async Task ActivateUserPersonaAsync(Guid userPersonaId)
    {
        if (IsBusy) return;
        try
        {
            await _repository.SetActiveUserPersonaAsync(userPersonaId);
            if (SelectedCharacter is not null) await LoadCharacterWorkspaceAsync(SelectedCharacter.Id);
            StatusText = $"Identidad activa: {SelectedUserPersona?.Name}. Memoria y relación cargadas por separado.";
        }
        catch (Exception ex) { StatusText = $"No pude cambiar de identidad: {ex.Message}"; }
    }

    private async Task LoadCharactersAsync(Guid? selectId = null, bool preserveNoSelection = false)
    {
        var current = selectId ?? SelectedCharacter?.Id;
        Characters.Clear();
        foreach (var item in await _repository.GetCharactersAsync())
            Characters.Add(new CharacterListItem(item.Id, item.Name, item.Age, item.Revision, item.UpdatedAt));
        SelectedCharacter = preserveNoSelection && current is null
            ? null
            : Characters.FirstOrDefault(x => x.Id == current) ?? Characters.FirstOrDefault();
    }

    private async Task LoadUniversesAsync(Guid? selectId = null)
    {
        var current = selectId ?? SelectedUniverse?.Id;
        Universes.Clear();
        foreach (var item in await _repository.GetUniversesAsync()) Universes.Add(new UniverseListItem(item.Id, item.Name));
        SelectedUniverse = Universes.FirstOrDefault(x => x.Id == current) ?? Universes.FirstOrDefault();
    }

    private async Task LoadUniverseAsync(Guid universeId)
    {
        var universe = await _repository.GetUniverseAsync(universeId);
        if (universe is null) return;
        _editingUniverseId = universe.Id;
        UniverseName = universe.Name;
        UniverseSummary = universe.Summary;
        UniverseCanon = universe.Canon;
        UniverseRules = universe.Rules;
        UniverseTone = universe.Tone;
        UniverseSource = universe.Source;
    }

    private async Task LoadCharacterWorkspaceAsync(Guid characterId, bool keepConversation = false)
    {
        if (SelectedUserPersona is null) return;
        var character = await _repository.GetCharacterAsync(characterId);
        if (character is null) return;
        _editingCharacterId = character.Id;
        CharacterName = character.Name; CharacterAge = character.Age; Appearance = character.Appearance;
        Backstory = character.Backstory; PersonalityNotes = character.PersonalityNotes; Profession = character.Profession;
        Likes = character.Likes; SpeechStyle = character.SpeechStyle; Goals = character.Goals; AffectiveStyle = character.AffectiveStyle;
        RomanticStyle = character.RomanticStyle; HumorStyle = character.HumorStyle; CharacterLanguage = character.Language;
        Romanticism = character.Romanticism; Extroversion = character.Extroversion; Humor = character.Humor; Calm = character.Calm;
        Confidence = character.Confidence; Affection = character.Affection; Independence = character.Independence;
        Initiative = character.Initiative; Formality = character.Formality; EmotionalIntensity = character.EmotionalIntensity;
        if (character.UniverseId is Guid universeId)
            SelectedUniverse = Universes.FirstOrDefault(x => x.Id == universeId) ?? SelectedUniverse;

        var selected = keepConversation ? SelectedConversation?.Id : null;
        await LoadConversationsAsync(characterId, SelectedUserPersona.Id, selected);
        await LoadMemoriesAsync(characterId, SelectedUserPersona.Id);
        await LoadDiaryAsync(characterId, SelectedUserPersona.Id);
        await LoadRelationshipAsync(characterId, SelectedUserPersona.Id);
        await LoadGalleryAsync(characterId);
    }

    private async Task LoadConversationsAsync(Guid characterId, Guid userPersonaId, Guid? selectId = null)
    {
        Conversations.Clear();
        foreach (var item in await _repository.GetConversationsAsync(characterId, userPersonaId))
            Conversations.Add(new ConversationListItem(item.Id, item.Title, item.SceneMode, item.UpdatedAt, item.ClosedAt is not null));
        SelectedConversation = Conversations.FirstOrDefault(x => x.Id == selectId) ?? Conversations.FirstOrDefault();
        if (SelectedConversation is null) Messages.Clear();
    }

    private async Task LoadConversationAsync(Guid conversationId)
    {
        var conversation = await _repository.GetConversationAsync(conversationId);
        if (conversation is null) return;
        SceneTitle = conversation.SceneTitle; ScenePremise = conversation.ScenePremise; SelectedSceneMode = conversation.SceneMode;
        ScenePlace = conversation.ScenePlace; SceneTime = conversation.SceneTime; SceneAtmosphere = conversation.SceneAtmosphere;
        SceneParticipants = conversation.SceneParticipants; SceneIntent = conversation.SceneIntent;
        SceneKnownFacts = conversation.SceneKnownFacts; SceneBoundaries = conversation.SceneBoundaries;
        Messages.Clear();
        foreach (var message in await _repository.GetMessagesAsync(conversationId, 120))
            Messages.Add(new ChatMessageViewModel(message.Role == MessageRole.User ? SelectedUserPersona?.Name ?? "Vos" : CharacterName, message.Content, message.Role == MessageRole.User));
    }

    private async Task LoadMemoriesAsync(Guid characterId, Guid userPersonaId)
    {
        Memories.Clear();
        foreach (var item in await _repository.GetMemoriesAsync(characterId, userPersonaId))
            Memories.Add(new MemoryListItem(item.Id, item.Kind, item.Title, item.Content, item.Importance, item.IsPinned, item.IsCanonical));
    }

    private async Task LoadDiaryAsync(Guid characterId, Guid userPersonaId)
    {
        Diary.Clear();
        foreach (var item in await _repository.GetDiaryAsync(characterId, userPersonaId)) Diary.Add(new DiaryListItem(item.Title, item.Content, item.CreatedAt));
    }

    private async Task LoadRelationshipAsync(Guid characterId, Guid userPersonaId)
    {
        var state = await _repository.GetRelationshipAsync(characterId, userPersonaId);
        RelationshipTrust = state.Trust * 100; RelationshipCloseness = state.Closeness * 100;
        RelationshipAffection = state.Affection * 100; RelationshipIntimacy = state.Intimacy * 100; RelationshipMood = state.CurrentMood;
    }

    private async Task LoadGalleryAsync(Guid characterId)
    {
        var previousAlbumId = AlbumItem?.Id;
        foreach (var item in Gallery) item.Dispose();
        Gallery.Clear();
        foreach (var item in await _repository.GetMediaAsync(characterId))
            Gallery.Add(new GalleryItemViewModel(item.Id, _mediaLibrary.GetAbsolutePath(item), item.Prompt, item.WorkflowName, item.CreatedAt));
        _mediaPresentation = await _repository.GetMediaPresentationAsync(characterId);
        PortraitZoom = _mediaPresentation.PortraitZoom;
        PortraitFocusX = _mediaPresentation.PortraitFocusX;
        PortraitFocusY = _mediaPresentation.PortraitFocusY;
        BackgroundZoom = _mediaPresentation.BackgroundZoom;
        BackgroundFocusX = _mediaPresentation.BackgroundFocusX;
        BackgroundFocusY = _mediaPresentation.BackgroundFocusY;
        SelectedGalleryItem = Gallery.FirstOrDefault(x => x.Id == previousAlbumId)
            ?? Gallery.FirstOrDefault(x => x.Id == _mediaPresentation.BackgroundMediaAssetId)
            ?? Gallery.FirstOrDefault();
        AlbumItem = SelectedGalleryItem;
        RefreshPresentationPreviews();
        OnPropertyChanged(nameof(HasGalleryItems));
    }

    private void RefreshPresentationPreviews()
    {
        var oldPortrait = CharacterPortraitPreview;
        CharacterPortraitPreview = null;
        oldPortrait?.Dispose();
        var oldBackground = ChatBackgroundPreview;
        ChatBackgroundPreview = null;
        oldBackground?.Dispose();

        if (_mediaPresentation is null) return;
        var portrait = Gallery.FirstOrDefault(x => x.Id == _mediaPresentation.PortraitMediaAssetId);
        var background = _mediaPresentation.BackgroundEnabled
            ? Gallery.FirstOrDefault(x => x.Id == _mediaPresentation.BackgroundMediaAssetId)
            : null;
        CharacterPortraitPreview = DecodeImage(portrait?.AbsolutePath, 360);
        ChatBackgroundPreview = DecodeImage(background?.AbsolutePath, 1280);
    }

    private static Bitmap? DecodeImage(string? path, int width)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, width);
        }
        catch { return null; }
    }

    private async Task LoadProvidersAsync(Guid? selectId = null)
    {
        var items = await _repository.GetProvidersAsync();
        var active = items.FirstOrDefault(x => x.IsActive);
        var selected = selectId ?? active?.Id ?? _provider?.Id;
        ActiveProviderText = active is null
            ? "Todavía no hay un motor activo."
            : $"{active.Name} · {(active.Location == ProviderLocation.Local ? "en este equipo" : "online")}";
        Providers.Clear();
        foreach (var item in items) Providers.Add(item);
        SelectedProviderProfile = Providers.FirstOrDefault(x => x.Id == selected) ?? Providers.FirstOrDefault();
        if (SelectedProviderProfile is not null) await LoadProviderFieldsAsync(SelectedProviderProfile);
    }

    private async Task LoadProviderFieldsAsync(ProviderProfile provider)
    {
        _provider = provider;
        LoadProviderFields(provider, await providerSecretStore.ExistsAsync(provider.Id));
        ProviderEditorTitle = provider.Location == ProviderLocation.Local
            ? "Motor local"
            : "Conexión online";
        ProviderEditorDescription = provider.Location == ProviderLocation.Local
            ? "Este perfil genera el texto en tu computadora."
            : "Este perfil usa un servidor externo, pero conserva historias, personajes e imágenes localmente.";
        StatusText = provider.Location == ProviderLocation.Local
            ? $"Perfil local «{provider.Name}» seleccionado."
            : $"Perfil online «{provider.Name}» seleccionado.";
    }

    private void LoadProviderFields(ProviderProfile provider, bool hasStoredToken)
    {
        _isLoadingProviderFields = true;
        ProviderName = provider.Name; SelectedProviderKind = provider.Kind; ProviderBaseUrl = provider.BaseUrl;
        ProviderModel = provider.Model; ProviderLaunchCommand = provider.LaunchCommand; ProviderAutoStart = provider.AutoStart;
        ProviderTemperature = provider.Temperature; ProviderMaxTokens = provider.MaxOutputTokens;
        SelectedProviderLocation = provider.Location;
        ProviderApiToken = string.Empty;
        HasStoredProviderToken = hasStoredToken;
        DetectedProviderModels.Clear();
        SelectedDetectedProviderModel = null;
        SelectedRemoteProviderPreset = provider.Location == ProviderLocation.Remote
            ? ProviderPresetCatalog.Match(provider.BaseUrl)
            : null;
        ProviderSetupHelp = SelectedRemoteProviderPreset?.Description
            ?? "Tu motor local continúa siendo la opción predeterminada y privada.";
        _isLoadingProviderFields = false;
        OnPropertyChanged(nameof(HasDetectedProviderModels));
        OnPropertyChanged(nameof(HasProviderHelpLink));
        OnPropertyChanged(nameof(IsCustomRemoteProvider));
        OnPropertyChanged(nameof(IsHostedRemoteProvider));
    }

    private bool TryBuildProviderDraft(out ProviderProfile provider)
    {
        provider = _provider ?? new ProviderProfile();
        if (string.IsNullOrWhiteSpace(ProviderName))
        {
            ProviderStatus = "Escribí un nombre para reconocer este motor.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(ProviderBaseUrl) || ProviderBaseUrl.Trim() is "https://" or "http://")
        {
            ProviderStatus = "Falta la dirección completa del servidor.";
            return false;
        }

        provider.Name = ProviderName.Trim();
        provider.Kind = SelectedProviderKind;
        provider.Location = SelectedProviderLocation;
        provider.BaseUrl = ProviderBaseUrl.Trim();
        provider.Model = ProviderModel.Trim();
        provider.LaunchCommand = IsLocalProvider ? ProviderLaunchCommand.Trim() : string.Empty;
        provider.AutoStart = IsLocalProvider && ProviderAutoStart;
        provider.Temperature = ProviderTemperature;
        provider.MaxOutputTokens = ProviderMaxTokens;
        _provider = provider;
        return true;
    }

    private async Task<ProviderProbe> ProbeDraftAsync(ProviderProfile provider, bool allowAutoStart)
    {
        var typedToken = ProviderApiToken.Trim();
        var previousToken = await providerSecretStore.GetAsync(provider.Id);
        var usingTemporaryToken = !string.IsNullOrWhiteSpace(typedToken);
        if (usingTemporaryToken) await providerSecretStore.SaveAsync(provider.Id, typedToken);
        try
        {
            return await application.ProbeProviderAsync(provider, allowAutoStart);
        }
        finally
        {
            if (usingTemporaryToken)
            {
                if (string.IsNullOrWhiteSpace(previousToken))
                    await providerSecretStore.DeleteAsync(provider.Id);
                else
                    await providerSecretStore.SaveAsync(provider.Id, previousToken);
            }
        }
    }

    private void UpdateDetectedModels(IReadOnlyList<string> models)
    {
        var current = ProviderModel.Trim();
        DetectedProviderModels.Clear();
        foreach (var model in models.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            DetectedProviderModels.Add(model);
        SelectedDetectedProviderModel = DetectedProviderModels.FirstOrDefault(x =>
            string.Equals(x, current, StringComparison.OrdinalIgnoreCase))
            ?? DetectedProviderModels.FirstOrDefault();
        OnPropertyChanged(nameof(HasDetectedProviderModels));
    }

    private async Task PersistProviderAsync(ProviderProfile provider)
    {
        await _repository.SaveProviderAsync(provider);
        if (!string.IsNullOrWhiteSpace(ProviderApiToken))
        {
            await providerSecretStore.SaveAsync(provider.Id, ProviderApiToken.Trim());
            ProviderApiToken = string.Empty;
        }
        await LoadProvidersAsync(provider.Id);
        StatusText = IsLocalProvider
            ? "Motor local guardado y activado."
            : "Motor online guardado y activado. El token permanece protegido por Windows.";
    }
}

public sealed record UniverseListItem(Guid Id, string Name);
public sealed record CharacterListItem(Guid Id, string Name, int Age, int Revision, DateTimeOffset UpdatedAt)
{
    public string DisplayName => $"{Name} · revisión {Revision}";
}
public sealed record UserPersonaListItem(Guid Id, string Name, int Age, string Pronouns, string Appearance,
    string About, string Profession, string Likes, string CommunicationStyle, string PortraitRelativePath, bool IsActive);
public sealed record ConversationListItem(Guid Id, string Title, SceneMode Mode, DateTimeOffset UpdatedAt, bool IsClosed);
public sealed record MemoryListItem(Guid Id, MemoryKind Kind, string Title, string Content, double Importance, bool IsPinned, bool IsCanonical);
public sealed record DiaryListItem(string Title, string Content, DateTimeOffset CreatedAt);

public sealed class GalleryItemViewModel : IDisposable
{
    public GalleryItemViewModel(Guid id, string absolutePath, string name, string source, DateTimeOffset createdAt)
    {
        Id = id;
        AbsolutePath = absolutePath;
        Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(absolutePath) : name;
        Source = source;
        CreatedAt = createdAt;
        try
        {
            using var stream = File.OpenRead(absolutePath);
            Preview = Bitmap.DecodeToWidth(stream, 360);
        }
        catch
        {
            Preview = null;
        }
    }

    public Guid Id { get; }
    public string AbsolutePath { get; }
    public string Name { get; }
    public string Source { get; }
    public DateTimeOffset CreatedAt { get; }
    public Bitmap? Preview { get; }

    public void Dispose() => Preview?.Dispose();
}

public partial class ChatMessageViewModel(string author, string content, bool isUser) : ObservableObject
{
    public string Author { get; } = author;
    public bool IsUser { get; } = isUser;
    [ObservableProperty] public partial string Content { get; set; } = content;
}
