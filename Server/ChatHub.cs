using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Text;

namespace Server
{
    public class ChatHub : Hub
    {
        private static int maxParticipants = 2;
        private static List<CLientDetail> participants = new List<CLientDetail>();
        private static EStatus currentStatus = EStatus.X;
        private static List<String> matchmakingRooms = new List<String>();
        private static List<String> manualRooms = new List<String>() { "Room1", "Room2", "Room3" };

        public async Task JoinGame(String room)
        {
            var listroom = participants.Where(p => p.Room == room).ToList();
            if (manualRooms.Contains(room) || matchmakingRooms.Contains(room))
            {
                if (listroom.Count < maxParticipants)
                {
                    bool typeroom;
                    if (manualRooms.Contains(room))
                    {
                        typeroom = true;
                    }
                    else typeroom = false;
                    var player = new CLientDetail() { ConnectionId = Context.ConnectionId, Room = room ,TypeRoom=typeroom};
                    participants.Add(player);
                    await Groups.AddToGroupAsync(Context.ConnectionId, room);
                    await Clients.Client(Context.ConnectionId).SendAsync("UserJoin", Context.ConnectionId);
                    listroom.Add(player);
                    await Clients.Group(listroom[0].Room).SendAsync("CompetitorJoin", Context.ConnectionId);
                    await Clients.Client(Context.ConnectionId).SendAsync("Notice", listroom.Count == 1 ? "You are X" : "You are O");
                    await LoadRoom();
                }
                else
                {
                    await Clients.Client(Context.ConnectionId).SendAsync("GameFull", "Phòng đã đầy");
                }
            }
        }

        public async Task LoadRoom()
        {
            List<Room> roomList = new List<Room>();
            for (int i = 0; i < manualRooms.Count; i++)
            {
                int listroom = participants.Where(p => p.Room == manualRooms[i]).Count();
                roomList.Add(new Room() { RoomName = manualRooms[i], Count = listroom });

            }
                    await Clients.All.SendAsync("Room", roomList);
        }

        public async Task CreateRoom(string RoomName)
        {
            manualRooms.Add(RoomName);
            await JoinGame(RoomName);
           
        }

        public async Task Matchmake()
        {
            var availableRoom = matchmakingRooms.FirstOrDefault(room => participants.Count(p => p.Room == room) < maxParticipants);

            if (availableRoom != null)
            {
                await JoinGame(availableRoom);
            }
            else
            {
                var newRoomName = GenerateRandomString(8);
                matchmakingRooms.Add(newRoomName);
                await JoinGame(newRoomName);
            }
        }

        public async Task Click(int x, int status)
        {
            var item = participants.Where(p => p.ConnectionId == Context.ConnectionId).FirstOrDefault();
            var listroom = participants.Where(p => p.Room == item.Room).ToList();
            if (listroom.Count < maxParticipants)
            {
                await Clients.Client(Context.ConnectionId).SendAsync("GameFull", "Phòng chưa đủ người");
                return;
            }

            var index = listroom.IndexOf(item);
            if ((index == 0 && currentStatus == EStatus.X) || (index == 1 && currentStatus == EStatus.O))
            {
                await Clients.Group(item.Room).SendAsync("ClickAtPoint", x, status);
                currentStatus = currentStatus == EStatus.X ? EStatus.O : EStatus.X;
                await Clients.Group(item.Room).SendAsync("ChangeTurn", currentStatus);
            }
        }

        public async Task Disconnect()
        {
            var item = participants.Where(p => p.ConnectionId == Context.ConnectionId).FirstOrDefault();
            if (item == null)
            {
                return;
            }
            participants.Remove(item);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, item.Room);
           
            await Clients.Groups(item.Room).SendAsync("LeaveGame", Context.ConnectionId);
            if (item.TypeRoom != true)
            {
                matchmakingRooms.Remove(item.Room);
                await Clients.Groups(item.Room).SendAsync("AutoDisConnect");
                
            }
            else
            {
                if (participants.Where(p => p.Room==item.Room).Count() == 0)
                {
                    manualRooms.Remove(item.Room);
                }
                
            }
            await LoadRoom();

        }

        public async Task SendMessage(String mess, string status)
        {
            var item = participants.Where(P => P.ConnectionId == Context.ConnectionId).FirstOrDefault();
            var listroom = participants.Where(p => p.Room == item.Room).ToList();
            if (status.Contains("X"))
            {
                await Clients.Group(item.Room).SendAsync("ReceiveMess", mess, "X");
            }
            else
            {
                await Clients.Group(item.Room).SendAsync("ReceiveMess", mess, "O");
            }
        }

        public enum EStatus
        {
            None, X, O
        }
        public class Room
        {
            public string RoomName { get; set; }
            public int Count { get; set; }
        }

        public string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder result = new StringBuilder();
            Random random = new Random();

            for (int i = 0; i < length; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }
    }

    public class CLientDetail
    {
        public String ConnectionId { get; set; }
        public String Room { get; set; }
        public bool TypeRoom { get; set; }

    }
}
