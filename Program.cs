/****************/
// Mã sinh viên: 202418929
// Họ tên: Hoàng Chí Kiệt
/****************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Lab06_BorrowingSystem
{
    // ================= 1. ENUMS & INTERFACE =================
    public enum DeviceStatus
    {
        Available,
        Borrowed,
        UnderMaintenance,
        Retired
    }

    public interface IBorrower
    {
        string BorrowerId { get; }
        string FullName { get; }
        int GetMaximumDevices();
    }

    public class Lecturer : IBorrower
    {
        public string BorrowerId { get; }
        public string FullName { get; }
        public Lecturer(string id, string name) { BorrowerId = id; FullName = name; }
        public int GetMaximumDevices() => 3;
    }

    public class TeachingAssistant : IBorrower
    {
        public string BorrowerId { get; }
        public string FullName { get; }
        public TeachingAssistant(string id, string name) { BorrowerId = id; FullName = name; }
        public int GetMaximumDevices() => 2;
    }

    // ================= 2. THIẾT BỊ (DEVICES) =================
    public abstract class Device
    {
        public string Id { get; private set; }
        public string Name { get; protected set; }
        public int Year { get; protected set; }
        public decimal Price { get; protected set; }
        public DeviceStatus Status { get; set; }
        
        // Giới hạn số ngày mượn của thiết bị
        public abstract int MaxBorrowDays { get; } 

        public Device(string id, string name, int year, decimal price, DeviceStatus status = DeviceStatus.Available)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Mã không rỗng.");
            if (price <= 0) throw new ArgumentException("Giá > 0.");
            if (year > DateTime.Now.Year) throw new ArgumentException("Năm không hợp lệ.");

            Id = id;
            Name = name;
            Year = year;
            Price = price;
            Status = status;
        }

        public abstract decimal CalculateMaintenanceCost();
        
        public void MarkAsBorrowed() { Status = DeviceStatus.Borrowed; }
        public void MarkAsAvailable() { Status = DeviceStatus.Available; }

        public override string ToString() => $"[{Id}] {Name} ({Status})";
    }

    public class ProjectorRemote : Device
    {
        public override int MaxBorrowDays => 7; 
        public ProjectorRemote(string id, string name, int year, decimal price) : base(id, name, year, price) { }
        public override decimal CalculateMaintenanceCost() => Price * 0.05m;
    }

    public class AirConRemote : Device
    {
        public override int MaxBorrowDays => 14;
        public AirConRemote(string id, string name, int year, decimal price) : base(id, name, year, price) { }
        public override decimal CalculateMaintenanceCost() => Price * 0.03m;
    }

    public class Microphone : Device
    {
        public override int MaxBorrowDays => 1; // Giới hạn mượn 1 ngày
        public Microphone(string id, string name, int year, decimal price) : base(id, name, year, price) { }
        public override decimal CalculateMaintenanceCost() => Price * 0.1m;
    }

    // ================= 3. EXCEPTIONS =================
    public class BorrowingException : Exception
    {
        public BorrowingException(string message) : base(message) { }
    }

    public class DeviceUnavailableException : BorrowingException
    {
        public string DeviceId { get; }
        public DeviceStatus CurrentStatus { get; }
        public DeviceUnavailableException(string deviceId, DeviceStatus status) 
            : base($"Thiết bị {deviceId} không sẵn sàng (Đang {status}).") 
        { DeviceId = deviceId; CurrentStatus = status; }
    }

    public class BorrowLimitExceededException : BorrowingException
    {
        public string BorrowerId { get; }
        public int MaximumDevices { get; }
        public BorrowLimitExceededException(string borrowerId, int max) 
            : base($"Người mượn {borrowerId} đã đạt giới hạn tối đa {max} thiết bị.") 
        { BorrowerId = borrowerId; MaximumDevices = max; }
    }

    public class InvalidBorrowPeriodException : BorrowingException
    {
        public InvalidBorrowPeriodException(string deviceId, int request, int max)
            : base($"Thiết bị {deviceId} chỉ cho phép mượn tối đa {max} ngày (Yêu cầu: {request}).") { }
    }

    public class DuplicateDeviceException : BorrowingException
    {
        public DuplicateDeviceException(string deviceId) : base($"Thiết bị {deviceId} đã có trong phiếu mượn.") { }
    }

    public class DeviceNotInSlipException : BorrowingException
    {
        public DeviceNotInSlipException(string deviceId) : base($"Thiết bị {deviceId} không nằm trong phiếu này.") { }
    }

    public class DeviceAlreadyReturnedException : BorrowingException
    {
        public DeviceAlreadyReturnedException(string deviceId) : base($"Thiết bị {deviceId} đã được trả trước đó rồi.") { }
    }


    // ================= 4. NGHIỆP VỤ MƯỢN TRẢ =================
    public class BorrowItem
    {
        public Device Device { get; }
        public DateTime BorrowedDate { get; }
        public int BorrowDays { get; }
        public DateTime? ReturnedDate { get; private set; }
        public bool IsReturned => ReturnedDate.HasValue;

        public BorrowItem(Device device, DateTime borrowedDate, int borrowDays)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            BorrowedDate = borrowedDate;
            BorrowDays = borrowDays;
            ReturnedDate = null;
        }

        public void Return(DateTime returnedDate)
        {
            if (IsReturned) throw new DeviceAlreadyReturnedException(Device.Id);
            if (returnedDate < BorrowedDate) throw new BorrowingException("Ngày trả không được trước ngày mượn.");
            ReturnedDate = returnedDate;
        }
    }

    public class BorrowingSlip
    {
        public string SlipId { get; }
        public IBorrower Borrower { get; }
        public string TeachingRoom { get; }
        public DateTime BorrowedDate { get; }
        
        private List<BorrowItem> _items = new List<BorrowItem>();
        public IReadOnlyList<BorrowItem> Items => _items.AsReadOnly();

        public BorrowingSlip(string slipId, IBorrower borrower, string room, DateTime date)
        {
            SlipId = slipId; Borrower = borrower; TeachingRoom = room; BorrowedDate = date;
        }

        public void AddDevice(Device device, int borrowDays)
        {
            // 1 & 2
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (borrowDays <= 0) throw new BorrowingException("Số ngày mượn phải dương.");
            // 3. Giới hạn thiết bị quy định
            if (borrowDays > device.MaxBorrowDays) throw new InvalidBorrowPeriodException(device.Id, borrowDays, device.MaxBorrowDays);
            // 4. Trùng thiết bị
            if (_items.Any(i => i.Device.Id == device.Id)) throw new DuplicateDeviceException(device.Id);
            // 5. Vượt giới hạn người mượn
            if (_items.Count >= Borrower.GetMaximumDevices()) throw new BorrowLimitExceededException(Borrower.BorrowerId, Borrower.GetMaximumDevices());
            // 6. Trạng thái
            if (device.Status != DeviceStatus.Available) throw new DeviceUnavailableException(device.Id, device.Status);

            // Ghi nhận
            var item = new BorrowItem(device, BorrowedDate, borrowDays);
            device.MarkAsBorrowed();
            _items.Add(item);
        }

        public void ReturnDevice(string deviceId, DateTime returnedDate)
        {
            var item = _items.FirstOrDefault(i => i.Device.Id == deviceId);
            if (item == null) throw new DeviceNotInSlipException(deviceId);

            item.Return(returnedDate);
            item.Device.MarkAsAvailable();
        }

        public void PrintSlip()
        {
            Console.WriteLine($"\n--- PHIẾU MƯỢN {SlipId} ({Borrower.FullName}) ---");
            foreach (var item in _items)
            {
                string retStatus = item.IsReturned ? $"Đã trả ({item.ReturnedDate:dd/MM/yyyy})" : "Chưa trả";
                Console.WriteLine($"- {item.Device.Name} | {retStatus}");
            }
        }
    }


    // ================= 5. KIỂM THỬ =================
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Lecturer gv = new Lecturer("GV01", "Kiet Hoang");
            TeachingAssistant ta = new TeachingAssistant("TA01", "Tro Giang A");

            ProjectorRemote prj = new ProjectorRemote("PRJ01", "Dieu khien may chieu Sony", 2023, 500000m);
            AirConRemote ac = new AirConRemote("AC01", "Dieu khien dieu hoa Pana", 2022, 300000m);
            Microphone mic = new Microphone("MIC01", "Micro khong day", 2024, 1500000m);
            Microphone mic2 = new Microphone("MIC02", "Micro co day", 2024, 800000m);
            
            BorrowingSlip slip1 = new BorrowingSlip("SLIP_01", gv, "P201", DateTime.Now);
            BorrowingSlip slipTA = new BorrowingSlip("SLIP_TA", ta, "P302", DateTime.Now);

            Console.WriteLine("====== 1. LUỒNG THÀNH CÔNG ======");
            try {
                slip1.AddDevice(prj, 2);
                slip1.AddDevice(mic, 1);
                Console.WriteLine("Thêm thiết bị thành công!");
                
                slip1.ReturnDevice("PRJ01", DateTime.Now.AddDays(1)); // Trả máy chiếu
                Console.WriteLine("Trả máy chiếu thành công!");
                slip1.PrintSlip();
            }
            catch (Exception ex) { Console.WriteLine("Lỗi: " + ex.Message); }

            Console.WriteLine("\n====== 2. TEST NGOẠI LỆ (EXCEPTIONS) ======");
            
            // Lỗi 1: Mượn thiết bị đang bảo trì
            try {
                ac.Status = DeviceStatus.UnderMaintenance;
                slip1.AddDevice(ac, 1);
            } catch (BorrowingException ex) { Console.WriteLine("[Bắt lỗi 1] " + ex.Message); }

            // Lỗi 2: Trợ giảng mượn quá số lượng (Giới hạn 2)
            try {
                slipTA.AddDevice(prj, 1);
                slipTA.AddDevice(mic, 1);
                slipTA.AddDevice(mic2, 1); // Cố mượn cái thứ 3
            } catch (BorrowingException ex) { Console.WriteLine("[Bắt lỗi 2] " + ex.Message); }

            // Lỗi 3: Thêm trùng thiết bị
            try {
                slip1.AddDevice(mic, 1);
            } catch (BorrowingException ex) { Console.WriteLine("[Bắt lỗi 3] " + ex.Message); }

            // Lỗi 4: Vượt ngày mượn cho phép của thiết bị (Mic chỉ cho mượn 1 ngày)
            try {
                slip1.AddDevice(mic2, 3);
            } catch (BorrowingException ex) { Console.WriteLine("[Bắt lỗi 4] " + ex.Message); }

            // Lỗi 5: Trả thiết bị không có trong phiếu
            try {
                slip1.ReturnDevice("AC01", DateTime.Now);
            } catch (BorrowingException ex) { Console.WriteLine("[Bắt lỗi 5] " + ex.Message); }

            // Lỗi 6: Trả thiết bị 2 lần
            try {
                slip1.ReturnDevice("PRJ01", DateTime.Now);
            } catch (BorrowingException ex) { Console.WriteLine("[Bắt lỗi 6] " + ex.Message); }
        }
    }
}