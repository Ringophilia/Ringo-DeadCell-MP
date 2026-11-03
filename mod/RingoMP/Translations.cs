using dc;
using dc.h2d;

namespace RingoMP
{
    /// Everything this mod says to a player.
    public enum Phrase
    {
        DoorWaiting, DoorCancelled, DoorPartnerHere, DoorPartnerElsewhere,
        YouAreDown, PartnerDown, PartnerRescuedYou, RescueTooLate,
        LinkLost, LinkBack, PartnerHere, PartnerLeft, MeetInNextLevel,
        OnlyHostStarts, StillSyncing,
        CreateRoomFailed, JoinRoomFailed, RemovedFromRoom,
        MenuSinglePlayer, MenuMultiplayer, MenuMultiplayerHelp,
        PanelRoom, PanelRooms,
        StateConnected, StateReconnecting, StateWaitingForPlayer, StateConnecting,
        HintLevel, HintPartnerReady, HintWaitingForHost, MemberHost,
        RoomLocked, RoomOpen, VisibilityPublic, VisibilityFriends,
        KeysStart, KeysOwner, KeysOwnerMore, KeysMember, KeysList,
        ListRefreshing, ListEmpty, ListPlaying, ListWaiting, NewRoomsPublic, NewRoomsFriends,
    }

    /// The game's own languages, each with its text in one column. What a language
    /// lacks falls back to English, and so does what its font cannot draw.
    public static class Translations
    {
        public static readonly string[] Languages = ["en", "de", "es", "fr", "it", "ja", "ko", "pl", "pt", "ru", "tr", "zh", "zh-tw"];

        /// Language variants that read the same.
        static readonly Dictionary<string, string> Aliases = new() { ["es-es"] = "es" };

        static readonly Dictionary<Phrase, string[]> Table = new()
        {
            [Phrase.DoorWaiting] = [
                "Waiting for your partner. Press again to cancel",
                "Warte auf deinen Partner. Zum Abbrechen erneut drücken",
                "Esperando a tu compañero. Pulsa otra vez para cancelar",
                "En attente de votre partenaire. Appuyez à nouveau pour annuler",
                "In attesa del compagno. Premi di nuovo per annullare",
                "仲間を待っています。もう一度押すとキャンセル",
                "동료를 기다리는 중. 다시 누르면 취소",
                "Czekasz na partnera. Naciśnij ponownie, aby anulować",
                "Aguardando seu parceiro. Pressione de novo para cancelar",
                "Ожидание напарника. Нажмите ещё раз для отмены",
                "Ortağın bekleniyor. İptal için tekrar bas",
                "等待队友进门  再按一次取消",
                "等待隊友進門  再按一次取消",
            ],
            [Phrase.DoorCancelled] = [
                "Cancelled", "Abgebrochen", "Cancelado", "Annulé", "Annullato", "キャンセルしました", "취소됨", "Anulowano", "Cancelado", "Отменено", "İptal edildi", "已取消进门", "已取消進門",
            ],
            [Phrase.DoorPartnerHere] = [
                "Your partner is at the door",
                "Dein Partner steht an der Tür",
                "Tu compañero está en la puerta",
                "Votre partenaire est à la porte",
                "Il compagno è alla porta",
                "仲間が扉の前にいます",
                "동료가 문 앞에 있습니다",
                "Partner czeka przy drzwiach",
                "Seu parceiro está na porta",
                "Напарник у двери",
                "Ortağın kapıda",
                "队友已在门口等待",
                "隊友已在門口等待",
            ],
            [Phrase.DoorPartnerElsewhere] = [
                "Your partner is at another door",
                "Dein Partner steht an einer anderen Tür",
                "Tu compañero está en otra puerta",
                "Votre partenaire est à une autre porte",
                "Il compagno è a un'altra porta",
                "仲間は別の扉にいます",
                "동료가 다른 문에 있습니다",
                "Partner czeka przy innych drzwiach",
                "Seu parceiro está em outra porta",
                "Напарник у другой двери",
                "Ortağın başka bir kapıda",
                "队友在另一扇门等待",
                "隊友在另一扇門等待",
            ],
            [Phrase.YouAreDown] = [
                "You are down. Wait for your partner",
                "Du bist am Boden. Warte auf deinen Partner",
                "Has caído. Espera a tu compañero",
                "Vous êtes à terre. Attendez votre partenaire",
                "Sei a terra. Aspetta il compagno",
                "倒れました。仲間を待ってください",
                "쓰러졌습니다. 동료를 기다리세요",
                "Padłeś. Poczekaj na partnera",
                "Você caiu. Aguarde seu parceiro",
                "Вы повержены. Ждите напарника",
                "Yere düştün. Ortağını bekle",
                "你倒下了  等待队友救援",
                "你倒下了  等待隊友救援",
            ],
            [Phrase.PartnerDown] = [
                "Your partner is down. Hold interact next to them",
                "Dein Partner ist am Boden. Halte daneben Interagieren",
                "Tu compañero ha caído. Mantén interactuar a su lado",
                "Votre partenaire est à terre. Maintenez Interagir près de lui",
                "Il compagno è a terra. Tieni premuto Interagisci accanto a lui",
                "仲間が倒れました。近くで調べるを長押し",
                "동료가 쓰러졌습니다. 옆에서 상호작용을 길게 누르세요",
                "Partner padł. Przytrzymaj interakcję obok niego",
                "Seu parceiro caiu. Segure interagir ao lado dele",
                "Напарник повержен. Удерживайте действие рядом с ним",
                "Ortağın düştü. Yanında etkileşimi basılı tut",
                "队友倒下了  靠近并按住交互键救援",
                "隊友倒下了  靠近並按住互動鍵救援",
            ],
            [Phrase.PartnerRescuedYou] = [
                "Your partner got you up",
                "Dein Partner hat dich aufgerichtet",
                "Tu compañero te ha levantado",
                "Votre partenaire vous a relevé",
                "Il compagno ti ha rialzato",
                "仲間が助けてくれました",
                "동료가 일으켜 주었습니다",
                "Partner cię podniósł",
                "Seu parceiro levantou você",
                "Напарник поднял вас",
                "Ortağın seni kaldırdı",
                "队友把你救起来了",
                "隊友把你救起來了",
            ],
            [Phrase.RescueTooLate] = [
                "Too late for rescue. You return in the next level",
                "Zu spät zur Rettung. Du kehrst im nächsten Level zurück",
                "Demasiado tarde. Volverás en el siguiente nivel",
                "Trop tard. Vous reviendrez au prochain niveau",
                "Troppo tardi. Tornerai nel prossimo livello",
                "救助の時間切れです。次のレベルで復帰します",
                "구조 시간이 끝났습니다. 다음 레벨에서 돌아옵니다",
                "Za późno. Wrócisz na następnym poziomie",
                "Tarde demais. Você volta na próxima fase",
                "Слишком поздно. Вы вернётесь на следующем уровне",
                "Çok geç. Sonraki bölümde döneceksin",
                "救援时间已过  下一关复活",
                "救援時間已過  下一關復活",
            ],
            [Phrase.LinkLost] = [
                "Lost your partner, reconnecting",
                "Verbindung verloren, verbinde neu",
                "Conexión perdida, reconectando",
                "Connexion perdue, reconnexion",
                "Connessione persa, riconnessione",
                "接続が切れました。再接続中",
                "연결이 끊겼습니다. 다시 연결 중",
                "Utracono połączenie, łączenie ponownie",
                "Conexão perdida, reconectando",
                "Связь потеряна, переподключение",
                "Bağlantı koptu, yeniden bağlanılıyor",
                "与队友断开  正在重连",
                "與隊友斷開  正在重連",
            ],
            [Phrase.LinkBack] = [
                "Your partner is back",
                "Dein Partner ist zurück",
                "Tu compañero ha vuelto",
                "Votre partenaire est de retour",
                "Il compagno è tornato",
                "仲間が戻りました",
                "동료가 돌아왔습니다",
                "Partner wrócił",
                "Seu parceiro voltou",
                "Напарник вернулся",
                "Ortağın geri döndü",
                "已重新连上队友",
                "已重新連上隊友",
            ],
            [Phrase.PartnerHere] = [
                "Your partner is here",
                "Dein Partner ist da",
                "Tu compañero ya está aquí",
                "Votre partenaire est là",
                "Il compagno è qui",
                "仲間が来ました",
                "동료가 도착했습니다",
                "Partner dołączył",
                "Seu parceiro chegou",
                "Напарник здесь",
                "Ortağın geldi",
                "队友已连接",
                "隊友已連線",
            ],
            [Phrase.PartnerLeft] = [
                "Your partner left. You go on alone",
                "Dein Partner ist weg. Du spielst allein weiter",
                "Tu compañero se fue. Sigues en solitario",
                "Votre partenaire est parti. Vous continuez seul",
                "Il compagno se n'è andato. Continui da solo",
                "仲間が去りました。一人で続けます",
                "동료가 나갔습니다. 혼자 계속합니다",
                "Partner odszedł. Grasz dalej sam",
                "Seu parceiro saiu. Você continua sozinho",
                "Напарник ушёл. Вы продолжаете один",
                "Ortağın ayrıldı. Tek başına devam ediyorsun",
                "队友已离开  你将继续单人游戏",
                "隊友已離開  你將繼續單人遊戲",
            ],
            [Phrase.MeetInNextLevel] = [
                "You will meet the host in their next level",
                "Du triffst den Host im nächsten Level",
                "Te reunirás con el anfitrión en su siguiente nivel",
                "Vous rejoindrez l'hôte au prochain niveau",
                "Raggiungerai l'host nel prossimo livello",
                "次のレベルでホストと合流します",
                "다음 레벨에서 방장과 합류합니다",
                "Dołączysz do hosta na następnym poziomie",
                "Você encontrará o anfitrião na próxima fase",
                "Вы встретитесь с хостом на следующем уровне",
                "Kurucuyla sonraki bölümde buluşacaksın",
                "等待房主进入下一关后汇合",
                "等待房主進入下一關後會合",
            ],
            [Phrase.OnlyHostStarts] = [
                "The host starts the run. Please wait for them",
                "Der Host startet den Lauf. Bitte warte",
                "El anfitrión inicia la partida. Espera, por favor",
                "L'hôte lance la partie. Veuillez patienter",
                "L'host avvia la partita. Attendi",
                "開始はホストが行います。お待ちください",
                "방장이 게임을 시작합니다. 기다려 주세요",
                "Grę rozpoczyna host. Poczekaj",
                "O anfitrião inicia a partida. Aguarde",
                "Забег начинает хост. Подождите",
                "Oyunu kurucu başlatır. Lütfen bekle",
                "开始游戏由房主决定  请等待房主开局",
                "開始遊戲由房主決定  請等待房主開局",
            ],
            [Phrase.StillSyncing] = [
                "Still syncing with your partner, one moment",
                "Synchronisiere mit deinem Partner, einen Moment",
                "Sincronizando con tu compañero, un momento",
                "Synchronisation en cours, un instant",
                "Sincronizzazione in corso, un momento",
                "仲間と同期中です。少々お待ちください",
                "동료와 동기화 중입니다. 잠시만 기다려 주세요",
                "Synchronizacja z partnerem, chwila",
                "Sincronizando com seu parceiro, um momento",
                "Синхронизация с напарником, подождите",
                "Ortağınla eşitleniyor, bir dakika",
                "正在与队友同步进度  请稍等",
                "正在與隊友同步進度  請稍等",
            ],
            [Phrase.CreateRoomFailed] = [
                "Could not create a room",
                "Raum konnte nicht erstellt werden",
                "No se pudo crear la sala",
                "Impossible de créer le salon",
                "Impossibile creare la stanza",
                "ルームを作成できませんでした",
                "방을 만들 수 없습니다",
                "Nie udało się utworzyć pokoju",
                "Não foi possível criar a sala",
                "Не удалось создать комнату",
                "Oda oluşturulamadı",
                "创建房间失败",
                "建立房間失敗",
            ],
            [Phrase.JoinRoomFailed] = [
                "Could not join: the room is full, locked or closed",
                "Beitritt fehlgeschlagen: Raum voll, gesperrt oder geschlossen",
                "No se pudo entrar: sala llena, bloqueada o cerrada",
                "Impossible de rejoindre : salon plein, verrouillé ou fermé",
                "Impossibile entrare: stanza piena, bloccata o chiusa",
                "参加できません。満員、ロック中、または閉鎖済みです",
                "참가할 수 없습니다. 방이 가득 찼거나 잠겼거나 닫혔습니다",
                "Nie można dołączyć: pokój pełny, zablokowany lub zamknięty",
                "Não foi possível entrar: sala cheia, trancada ou fechada",
                "Не удалось войти: комната заполнена, заперта или закрыта",
                "Katılınamadı: oda dolu, kilitli veya kapalı",
                "无法加入: 房间已满、已锁定或已关闭",
                "無法加入: 房間已滿、已鎖定或已關閉",
            ],
            [Phrase.RemovedFromRoom] = [
                "The host removed you from the room",
                "Der Host hat dich aus dem Raum entfernt",
                "El anfitrión te ha expulsado de la sala",
                "L'hôte vous a retiré du salon",
                "L'host ti ha rimosso dalla stanza",
                "ホストにルームから退出させられました",
                "방장이 방에서 내보냈습니다",
                "Host usunął cię z pokoju",
                "O anfitrião removeu você da sala",
                "Хост удалил вас из комнаты",
                "Kurucu seni odadan çıkardı",
                "你已被房主移出房间",
                "你已被房主移出房間",
            ],
            [Phrase.MenuSinglePlayer] = [
                "Single player", "Einzelspieler", "Un jugador", "Solo", "Giocatore singolo", "シングルプレイ", "싱글 플레이", "Jeden gracz", "Um jogador", "Одиночная игра", "Tek oyunculu", "单人游戏", "單人遊戲",
            ],
            [Phrase.MenuMultiplayer] = [
                "Multiplayer", "Mehrspieler", "Multijugador", "Multijoueur", "Multigiocatore", "マルチプレイ", "멀티 플레이", "Wielu graczy", "Multijogador", "Мультиплеер", "Çok oyunculu", "多人游戏", "多人遊戲",
            ],
            [Phrase.MenuMultiplayerHelp] = [
                "Create or join a room and play with a friend",
                "Erstelle einen Raum oder tritt bei und spiele mit einem Freund",
                "Crea o únete a una sala y juega con un amigo",
                "Créez ou rejoignez un salon et jouez avec un ami",
                "Crea o entra in una stanza e gioca con un amico",
                "ルームを作成または参加して友達と遊びます",
                "방을 만들거나 참가해 친구와 함께 플레이합니다",
                "Utwórz pokój lub dołącz i graj ze znajomym",
                "Crie ou entre em uma sala e jogue com um amigo",
                "Создайте комнату или войдите в неё и играйте с другом",
                "Oda kur veya katıl, arkadaşınla oyna",
                "创建或加入房间  和朋友一起玩",
                "建立或加入房間  和朋友一起玩",
            ],
            [Phrase.PanelRoom] = [
                "Co-op room", "Koop-Raum", "Sala cooperativa", "Salon coopératif", "Stanza cooperativa", "協力ルーム", "협동 방", "Pokój kooperacji", "Sala cooperativa", "Комната", "Ortak oda", "联机房间", "連線房間",
            ],
            [Phrase.PanelRooms] = [
                "Co-op rooms", "Koop-Räume", "Salas cooperativas", "Salons coopératifs", "Stanze cooperative", "協力ルーム一覧", "협동 방 목록", "Pokoje kooperacji", "Salas cooperativas", "Комнаты", "Ortak odalar", "联机房间列表", "連線房間列表",
            ],
            [Phrase.StateConnected] = [
                "Connected", "Verbunden", "Conectado", "Connecté", "Connesso", "接続済み", "연결됨", "Połączono", "Conectado", "Подключено", "Bağlandı", "已连接", "已連線",
            ],
            [Phrase.StateReconnecting] = [
                "Link lost, reconnecting...", "Verbindung verloren, verbinde neu...", "Conexión perdida, reconectando...", "Connexion perdue, reconnexion...", "Connessione persa, riconnessione...",
                "接続が切れました。再接続中...", "연결 끊김, 다시 연결 중...", "Utracono połączenie, łączenie...", "Conexão perdida, reconectando...", "Связь потеряна, переподключение...", "Bağlantı koptu, yeniden bağlanılıyor...", "连接中断  正在重连...", "連線中斷  正在重連...",
            ],
            [Phrase.StateWaitingForPlayer] = [
                "Waiting for a player", "Warte auf einen Spieler", "Esperando a un jugador", "En attente d'un joueur", "In attesa di un giocatore", "プレイヤーを待っています", "플레이어를 기다리는 중", "Czekanie na gracza", "Aguardando um jogador", "Ожидание игрока", "Oyuncu bekleniyor", "等待玩家加入", "等待玩家加入",
            ],
            [Phrase.StateConnecting] = [
                "Connecting to the host...", "Verbinde mit dem Host...", "Conectando con el anfitrión...", "Connexion à l'hôte...", "Connessione all'host...", "ホストに接続中...", "방장에게 연결 중...", "Łączenie z hostem...", "Conectando ao anfitrião...", "Подключение к хосту...", "Kurucuya bağlanılıyor...", "正在连接房主...", "正在連線房主...",
            ],
            [Phrase.HintLevel] = [
                "Level: {0}", "Level: {0}", "Nivel: {0}", "Niveau : {0}", "Livello: {0}", "レベル: {0}", "레벨: {0}", "Poziom: {0}", "Fase: {0}", "Уровень: {0}", "Bölüm: {0}", "当前关卡: {0}", "目前關卡: {0}",
            ],
            [Phrase.HintPartnerReady] = [
                "Your partner is ready, you can start",
                "Dein Partner ist bereit, du kannst starten",
                "Tu compañero está listo, ya puedes empezar",
                "Votre partenaire est prêt, vous pouvez commencer",
                "Il compagno è pronto, puoi iniziare",
                "仲間の準備ができました。開始できます",
                "동료가 준비되었습니다. 시작할 수 있습니다",
                "Partner gotowy, możesz zacząć",
                "Seu parceiro está pronto, você pode começar",
                "Напарник готов, можно начинать",
                "Ortağın hazır, başlayabilirsin",
                "队友已就绪  可以开始游戏",
                "隊友已準備好  可以開始遊戲",
            ],
            [Phrase.HintWaitingForHost] = [
                "Waiting for the host to start", "Warte auf den Start durch den Host", "Esperando a que el anfitrión empiece", "En attente du lancement par l'hôte", "In attesa che l'host inizi", "ホストの開始を待っています", "방장이 시작하기를 기다리는 중", "Czekanie na start hosta", "Aguardando o anfitrião iniciar", "Ожидание запуска хостом", "Kurucunun başlatması bekleniyor", "等待房主开始游戏", "等待房主開始遊戲",
            ],
            [Phrase.MemberHost] = [
                "{0} (host)", "{0} (Host)", "{0} (anfitrión)", "{0} (hôte)", "{0} (host)", "{0} (ホスト)", "{0} (방장)", "{0} (host)", "{0} (anfitrião)", "{0} (хост)", "{0} (kurucu)", "{0} (房主)", "{0} (房主)",
            ],
            [Phrase.RoomLocked] = [
                "Room: locked", "Raum: gesperrt", "Sala: bloqueada", "Salon : verrouillé", "Stanza: bloccata", "ルーム: ロック中", "방: 잠김", "Pokój: zablokowany", "Sala: trancada", "Комната: заперта", "Oda: kilitli", "房间: 已锁定", "房間: 已鎖定",
            ],
            [Phrase.RoomOpen] = [
                "Room: open", "Raum: offen", "Sala: abierta", "Salon : ouvert", "Stanza: aperta", "ルーム: 公開中", "방: 열림", "Pokój: otwarty", "Sala: aberta", "Комната: открыта", "Oda: açık", "房间: 开放", "房間: 開放",
            ],
            [Phrase.VisibilityPublic] = [
                "Public", "Öffentlich", "Pública", "Public", "Pubblica", "全員に公開", "공개", "Publiczny", "Pública", "Для всех", "Herkese açık", "所有人可见", "所有人可見",
            ],
            [Phrase.VisibilityFriends] = [
                "Friends only", "Nur Freunde", "Solo amigos", "Amis uniquement", "Solo amici", "フレンドのみ", "친구만", "Tylko znajomi", "Só amigos", "Только друзья", "Yalnızca arkadaşlar", "仅好友可见", "僅好友可見",
            ],
            [Phrase.KeysStart] = [
                "[S] New run  [C] Continue save",
                "[S] Neuer Lauf  [C] Spielstand fortsetzen",
                "[S] Nueva partida  [C] Continuar partida",
                "[S] Nouvelle partie  [C] Continuer",
                "[S] Nuova partita  [C] Continua",
                "[S] 新規プレイ  [C] 続きから",
                "[S] 새 게임  [C] 이어하기",
                "[S] Nowa gra  [C] Kontynuuj",
                "[S] Novo jogo  [C] Continuar",
                "[S] Новый забег  [C] Продолжить",
                "[S] Yeni oyun  [C] Devam et",
                "[S] 开始新游戏  [C] 继续存档",
                "[S] 開始新遊戲  [C] 繼續存檔",
            ],
            [Phrase.KeysOwner] = [
                "[I] Invite  [K] Remove player  [L] Lock/unlock",
                "[I] Einladen  [K] Spieler entfernen  [L] Sperren",
                "[I] Invitar  [K] Expulsar  [L] Bloquear",
                "[I] Inviter  [K] Retirer le joueur  [L] Verrouiller",
                "[I] Invita  [K] Rimuovi giocatore  [L] Blocca",
                "[I] 招待  [K] 退出させる  [L] ロック切替",
                "[I] 초대  [K] 내보내기  [L] 잠금 전환",
                "[I] Zaproś  [K] Usuń gracza  [L] Zablokuj",
                "[I] Convidar  [K] Remover jogador  [L] Trancar",
                "[I] Пригласить  [K] Удалить игрока  [L] Запереть",
                "[I] Davet et  [K] Oyuncuyu çıkar  [L] Kilitle",
                "[I] 邀请好友  [K] 移出玩家  [L] 锁定/解锁",
                "[I] 呼叫好友  [K] 移出玩家  [L] 鎖定/解鎖",
            ],
            [Phrase.KeysOwnerMore] = [
                "[P] Visibility  [X] Close room  [Esc] Back",
                "[P] Sichtbarkeit  [X] Raum schließen  [Esc] Zurück",
                "[P] Visibilidad  [X] Cerrar sala  [Esc] Volver",
                "[P] Visibilité  [X] Fermer le salon  [Esc] Retour",
                "[P] Visibilità  [X] Chiudi stanza  [Esc] Indietro",
                "[P] 公開範囲  [X] ルームを閉じる  [Esc] 戻る",
                "[P] 공개 범위  [X] 방 닫기  [Esc] 뒤로",
                "[P] Widoczność  [X] Zamknij pokój  [Esc] Wróć",
                "[P] Visibilidade  [X] Fechar sala  [Esc] Voltar",
                "[P] Видимость  [X] Закрыть комнату  [Esc] Назад",
                "[P] Görünürlük  [X] Odayı kapat  [Esc] Geri",
                "[P] 切换可见范围  [X] 关闭房间  [Esc] 返回",
                "[P] 切換可見範圍  [X] 關閉房間  [Esc] 返回",
            ],
            [Phrase.KeysMember] = [
                "[I] Invite  [X] Leave room  [Esc] Back",
                "[I] Einladen  [X] Raum verlassen  [Esc] Zurück",
                "[I] Invitar  [X] Salir de la sala  [Esc] Volver",
                "[I] Inviter  [X] Quitter le salon  [Esc] Retour",
                "[I] Invita  [X] Esci dalla stanza  [Esc] Indietro",
                "[I] 招待  [X] ルームを出る  [Esc] 戻る",
                "[I] 초대  [X] 방 나가기  [Esc] 뒤로",
                "[I] Zaproś  [X] Opuść pokój  [Esc] Wróć",
                "[I] Convidar  [X] Sair da sala  [Esc] Voltar",
                "[I] Пригласить  [X] Покинуть комнату  [Esc] Назад",
                "[I] Davet et  [X] Odadan çık  [Esc] Geri",
                "[I] 邀请好友  [X] 退出房间  [Esc] 返回",
                "[I] 呼叫好友  [X] 退出房間  [Esc] 返回",
            ],
            [Phrase.KeysList] = [
                "[N] New room  [R] Refresh  [P] Visibility  [Esc] Back",
                "[N] Neuer Raum  [R] Aktualisieren  [P] Sichtbarkeit  [Esc] Zurück",
                "[N] Nueva sala  [R] Actualizar  [P] Visibilidad  [Esc] Volver",
                "[N] Nouveau salon  [R] Actualiser  [P] Visibilité  [Esc] Retour",
                "[N] Nuova stanza  [R] Aggiorna  [P] Visibilità  [Esc] Indietro",
                "[N] 新規ルーム  [R] 更新  [P] 公開範囲  [Esc] 戻る",
                "[N] 새 방  [R] 새로고침  [P] 공개 범위  [Esc] 뒤로",
                "[N] Nowy pokój  [R] Odśwież  [P] Widoczność  [Esc] Wróć",
                "[N] Nova sala  [R] Atualizar  [P] Visibilidade  [Esc] Voltar",
                "[N] Новая комната  [R] Обновить  [P] Видимость  [Esc] Назад",
                "[N] Yeni oda  [R] Yenile  [P] Görünürlük  [Esc] Geri",
                "[N] 新建房间  [R] 刷新  [P] 切换可见范围  [Esc] 返回",
                "[N] 新建房間  [R] 重新整理  [P] 切換可見範圍  [Esc] 返回",
            ],
            [Phrase.ListRefreshing] = [
                "Refreshing...", "Aktualisiere...", "Actualizando...", "Actualisation...", "Aggiornamento...", "更新中...", "새로고침 중...", "Odświeżanie...", "Atualizando...", "Обновление...", "Yenileniyor...", "正在刷新...", "正在重新整理...",
            ],
            [Phrase.ListEmpty] = [
                "No rooms found", "Keine Räume gefunden", "No se encontraron salas", "Aucun salon trouvé", "Nessuna stanza trovata", "ルームが見つかりません", "방을 찾을 수 없습니다", "Nie znaleziono pokoi", "Nenhuma sala encontrada", "Комнаты не найдены", "Oda bulunamadı", "没有找到房间", "沒有找到房間",
            ],
            [Phrase.ListPlaying] = [
                "playing {0}", "spielt {0}", "jugando {0}", "en jeu : {0}", "in gioco {0}", "プレイ中 {0}", "플레이 중 {0}", "w grze {0}", "jogando {0}", "в игре {0}", "oyunda {0}", "游戏中 {0}", "遊戲中 {0}",
            ],
            [Phrase.ListWaiting] = [
                "waiting", "wartet", "esperando", "en attente", "in attesa", "待機中", "대기 중", "oczekuje", "aguardando", "ожидание", "bekliyor", "等待中", "等待中",
            ],
            [Phrase.NewRoomsPublic] = [
                "New rooms: public", "Neue Räume: öffentlich", "Salas nuevas: públicas", "Nouveaux salons : publics", "Nuove stanze: pubbliche", "新規ルーム: 全員に公開", "새 방: 공개", "Nowe pokoje: publiczne", "Novas salas: públicas", "Новые комнаты: для всех", "Yeni odalar: herkese açık", "新房间: 所有人可见", "新房間: 所有人可見",
            ],
            [Phrase.NewRoomsFriends] = [
                "New rooms: friends only", "Neue Räume: nur Freunde", "Salas nuevas: solo amigos", "Nouveaux salons : amis uniquement", "Nuove stanze: solo amici", "新規ルーム: フレンドのみ", "새 방: 친구만", "Nowe pokoje: tylko znajomi", "Novas salas: só amigos", "Новые комнаты: только друзья", "Yeni odalar: yalnızca arkadaşlar", "新房间: 仅好友可见", "新房間: 僅好友可見",
            ],
        };

        static string Current
        {
            get
            {
                var language = Lang.Class.LANG?.ToString() ?? "en";
                return Aliases.GetValueOrDefault(language, language);
            }
        }

        /// A phrase in a given language, whatever the game runs in.
        public static string In(string language, Phrase phrase) =>
            Table[phrase][System.Math.Max(System.Array.IndexOf(Languages, language), 0)];

        public static IEnumerable<Phrase> Phrases => Table.Keys;

        /// A phrase in the game's language, or in English if its font lacks a letter.
        public static string Get(Phrase phrase, params object[] values)
        {
            var text = In(Current, phrase);
            if (!CanDraw(text))
            {
                text = In("en", phrase);
            }
            return string.Format(text, values);
        }

        /// What came from outside the game, like a player's name, may hold letters
        /// the font lacks. They would simply vanish, so they show as a question mark.
        public static string Drawable(string text) =>
            new([.. text.Select(letter => Font?.hasChar(letter) != false ? letter : '?')]);

        static Font? Font => Assets.Class.font18;

        static bool CanDraw(string text) => Font == null || text.All(letter => letter == '{' || letter == '}' || Font.hasChar(letter));
    }
}
