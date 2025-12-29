
using SMTScheduler.Models;
using SMTScheduler.Services;


    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║       HỆ THỐNG LẬP LỊCH SẢN XUẤT SMT - OR-TOOLS          ║");
    Console.WriteLine("║       (Phiên bản đơn giản với Stage Naming)               ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // ═══════════════════════════════════════════════════════════════
    // 1. ĐỊNH NGHĨA CÔNG ĐOẠN
    // ═══════════════════════════════════════════════════════════════
    var stages = new List<Stage>
            {
                new Stage { Id = 1, Name = "Solder Paste", Order = 1 },
                new Stage { Id = 2, Name = "Pick & Place", Order = 2 },
                new Stage { Id = 3, Name = "Reflow", Order = 3 },
                new Stage { Id = 4, Name = "AOI", Order = 4 }
            };

    Console.WriteLine("═══ CÔNG ĐOẠN ═══");
    foreach (var s in stages)
    {
        Console.WriteLine($"  CĐ{s.Order}: {s.Name}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. ĐỊNH NGHĨA LINES (DÂY CHUYỀN)
    // ═══════════════════════════════════════════════════════════════
    var lines = new List<Line>();

    // Line 1: Hỗ trợ tất cả 4 công đoạn
    var line1 = new Line("L001", "Line 1 - Samsung");
    line1.AddStageCapability(1, efficiency: 0.90);
    line1.AddStageCapability(2, efficiency: 0.85);
    line1.AddStageCapability(3, efficiency: 0.95);
    line1.AddStageCapability(4, efficiency: 0.90);
    lines.Add(line1);

    // Line 2: Hỗ trợ tất cả 4 công đoạn
    var line2 = new Line("L002", "Line 2 - Juki");
    line2.AddStageCapability(1, efficiency: 0.85);
    line2.AddStageCapability(2, efficiency: 0.80);
    line2.AddStageCapability(3, efficiency: 0.90);
    line2.AddStageCapability(4, efficiency: 0.85);
    lines.Add(line2);

    // Line 3: Hỗ trợ tất cả 4 công đoạn
    var line3 = new Line("L003", "Line 3 - Yamaha");
    line3.AddStageCapability(1, efficiency: 0.80);
    line3.AddStageCapability(2, efficiency: 0.90);
    line3.AddStageCapability(3, efficiency: 0.85);
    line3.AddStageCapability(4, efficiency: 0.80);
    lines.Add(line3);

    Console.WriteLine("\n═══ LINES ═══");
    foreach (var l in lines)
    {
        Console.WriteLine($"  {l.Name}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. ĐỊNH NGHĨA LỊCH LÀM VIỆC
    // ═══════════════════════════════════════════════════════════════
    DateTime today = DateTime.Today;
    var calendar = new WorkingCalendar();

    calendar.WorkingDays = new List<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            };

    calendar.DefaultShift = new WorkShift("Ca ngày",
        startTime: new TimeSpan(8, 0, 0),
        endTime: new TimeSpan(17, 0, 0),
        breakMinutes: 60);

    calendar.AddWeekendsAsHolidays(today, today.AddMonths(2));

    Console.WriteLine($"\n═══ LỊCH LÀM VIỆC ═══");
    Console.WriteLine($"  Ca: {calendar.DefaultShift}");
    Console.WriteLine($"  Ngày làm việc: Thứ 2 - Thứ 6");

    // ═══════════════════════════════════════════════════════════════
    // 4. ĐỊNH NGHĨA SẢN PHẨM VỚI STAGE NAMING
    //    Model đổi tên qua công đoạn: A → A2 → A3 → A4
    // ═══════════════════════════════════════════════════════════════
    var products = new List<Product>();

    // ─────────────────────────────────────────────────────────────
    // Model A: 500 sản phẩm, deadline 3 ngày
    // Tên qua công đoạn: A → A2 → A3 → A4
    // ─────────────────────────────────────────────────────────────
    var modelA = new Product
    {
        Id = "A",
        Name = "A",
        OrderQuantity = 500,
        StockQuantity = 0,
        StartDate = today,
        DueDate = today.AddDays(3),
        Priority = 1
    };
    // *** KHAI BÁO TÊN MODEL QUA TỪNG CÔNG ĐOẠN ***
    modelA.SetStageNames(new Dictionary<int, string>
            {
                { 1, "A" },   // Công đoạn 1: A
                { 2, "A2" },  // Công đoạn 2: A2
                { 3, "A3" },  // Công đoạn 3: A3
                { 4, "A4" }   // Công đoạn 4: A4
            });
    // *** LOT SPLITTING THEO TỪNG CÔNG ĐOẠN ***
    // CĐ1: batch 250 (in kem hàn nhanh)
    // CĐ2: batch 100 (P&P chậm, chia nhỏ)
    // CĐ3: batch 200 (Reflow trung bình)
    // CĐ4: batch 250 (AOI nhanh)
    modelA.SetStageLotSplitting(new Dictionary<int, int>
            {
                { 1, 250 },  // CĐ1: batch 250 sp
                { 2, 100 },  // CĐ2: batch 100 sp (chia nhỏ vì P&P chậm)
                { 3, 200 },  // CĐ3: batch 200 sp
                { 4, 250 }   // CĐ4: batch 250 sp
            }, minGapMinutes: 5);
    products.Add(modelA);

    // ─────────────────────────────────────────────────────────────
    // Model B: 300 sản phẩm, deadline 4 ngày
    // Tên qua công đoạn: B → B2 → B3 → B4
    // ─────────────────────────────────────────────────────────────
    var modelB = new Product
    {
        Id = "B",
        Name = "B",
        OrderQuantity = 300,
        StockQuantity = 0,
        StartDate = today,
        DueDate = today.AddDays(4),
        Priority = 2
    };
    modelB.SetStageNames(new Dictionary<int, string>
            {
                { 1, "B" },
                { 2, "B2" },
                { 3, "B3" },
                { 4, "B4" }
            });
    // *** LOT SPLITTING THEO CÔNG ĐOẠN ***
    // Chỉ chia nhỏ ở CĐ2 (P&P), các CĐ khác chạy liên tục
    modelB.SetStageLotSplitting(2, batchSize: 100, minGapMinutes: 3);  // Chỉ CĐ2
    products.Add(modelB);

    // ─────────────────────────────────────────────────────────────
    // Model C: 400 sản phẩm, deadline 4 ngày
    // Dùng Pattern tự động: C-1 → C-2 → C-3 → C-4
    // ─────────────────────────────────────────────────────────────
    var modelC = new Product
    {
        Id = "C",
        Name = "C",
        OrderQuantity = 400,
        StockQuantity = 0,
        StartDate = today,
        DueDate = today.AddDays(4),
        Priority = 2
    };
    // *** DÙNG PATTERN TỰ ĐỘNG TẠO TÊN ***
    modelC.StageNamePattern = "{Name}-{StageOrder}";
    // Model C: KHÔNG chia lot (sản xuất liên tục 400 sp)
    modelC.DisableLotSplitting();
    products.Add(modelC);

    // ─────────────────────────────────────────────────────────────
    // Model D: 350 sản phẩm, deadline 5 ngày
    // Chỉ 3 công đoạn (bỏ AOI): D → D2 → D3
    // ─────────────────────────────────────────────────────────────
    var modelD = new Product
    {
        Id = "D",
        Name = "D",
        OrderQuantity = 350,
        StockQuantity = 0,
        StartDate = today,
        DueDate = today.AddDays(5),
        Priority = 3
    };
    modelD.SetStageNames(new Dictionary<int, string>
            {
                { 1, "D" },
                { 2, "D2" },
                { 3, "D3" }
                // Không có công đoạn 4 (AOI)
            });
    // *** LOT SPLITTING THEO CÔNG ĐOẠN ***
    modelD.SetStageLotSplitting(new Dictionary<int, int>
            {
                { 1, 150 },  // CĐ1: batch 150 sp
                { 2, 80 },   // CĐ2: batch 80 sp
                { 3, 120 }   // CĐ3: batch 120 sp
            });
    products.Add(modelD);

    // ─────────────────────────────────────────────────────────────
    // Model E: 250 sản phẩm, deadline 5 ngày
    // Tên qua công đoạn: E → E2 → E3 → E4
    // ─────────────────────────────────────────────────────────────
    var modelE = new Product
    {
        Id = "E",
        Name = "E",
        OrderQuantity = 250,
        StockQuantity = 0,
        StartDate = today,
        DueDate = today.AddDays(5),
        Priority = 3
    };
    modelE.SetStageNames(new Dictionary<int, string>
            {
                { 1, "E" },
                { 2, "E2" },
                { 3, "E3" },
                { 4, "E4" }
            });
    // Model E: KHÔNG chia lot (số lượng nhỏ)
    products.Add(modelE);

    // Hiển thị sản phẩm với Stage Naming
    Console.WriteLine("\n═══ SẢN PHẨM VỚI STAGE NAMING & LOT SPLITTING ═══");
    foreach (var p in products)
    {
        Console.WriteLine($"\n  Model {p.Name}: {p.RequiredQuantity} sp, Deadline: {p.DueDate:dd/MM/yyyy}, Priority: {p.Priority}");

        // Hiển thị tên qua từng công đoạn
        Console.Write("    Tên qua CĐ: ");
        for (int stageOrder = 1; stageOrder <= 4; stageOrder++)
        {
            var stage = stages.Find(s => s.Order == stageOrder);
            if (stage != null)
            {
                string nameAtStage = p.GetNameAtStage(stage.Id, stageOrder);
                Console.Write($"CĐ{stageOrder}={nameAtStage}");
                if (stageOrder < 4) Console.Write(" → ");
            }
        }
        Console.WriteLine();

        // Hiển thị Lot Splitting theo công đoạn
        Console.WriteLine("    Lot Splitting:");
        bool hasAnySplitting = false;
        for (int stageId = 1; stageId <= 4; stageId++)
        {
            var lotConfig = p.GetLotConfigForStage(stageId);
            if (lotConfig.EnableSplitting)
            {
                var batches = lotConfig.CalculateBatches(p.RequiredQuantity);
                Console.WriteLine($"      CĐ{stageId}: {batches.Count} batch ({string.Join(", ", batches)} sp)");
                hasAnySplitting = true;
            }
        }
        if (!hasAnySplitting)
        {
            Console.WriteLine($"      Không chia (chạy liên tục {p.RequiredQuantity} sp)");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. TẠO SCHEDULER
    // ═══════════════════════════════════════════════════════════════
    var scheduler = new SMTSchedulerService(
        stages,
        lines,
        products,
        today,
        calendar);

    // *** CẤU HÌNH - TẮT các tính năng không cần ***
    scheduler.EnableComponentGrouping = false;  // TẮT - không dùng Component
    scheduler.EnableOperatorManagement = false; // TẮT - không dùng Operator
    scheduler.UseManualGrouping = false;        // TẮT - không dùng Manual Group
    scheduler.EnableSuggestions = false;        // TẮT - không gợi ý OT

    // *** BẬT Lot Splitting ***
    scheduler.EnableLotSplitting = true;        // BẬT - cho phép chia lot

    // *** CẤU HÌNH - BẬT các tính năng cần ***
    scheduler.EnableStageNaming = true;         // BẬT - Model đổi tên qua CĐ
    scheduler.EnableCustomRouting = true;       // BẬT - Routing riêng cho từng model
    scheduler.EnableStageTransferTime = true;   // BẬT - Thời gian transfer giữa công đoạn

    // ═══════════════════════════════════════════════════════════════
    // 6. CẤU HÌNH THỜI GIAN TRANSFER GIỮA CÁC CÔNG ĐOẠN
    // ═══════════════════════════════════════════════════════════════
    Console.WriteLine("\n═══ THỜI GIAN TRANSFER GIỮA CÔNG ĐOẠN ═══");

    // Lấy StageTransferMatrix và cấu hình
    var stageTransfer = scheduler.StageTransferMatrix;

    // CĐ1 → CĐ2: 3 phút (chờ kiểm tra sau in kem hàn)
    stageTransfer.AddTransfer(1, 2, 3, "Chờ kiểm tra sau in kem hàn");
    Console.WriteLine("  CĐ1 → CĐ2: 3 phút (kiểm tra kem hàn)");

    // CĐ2 → CĐ3: 2 phút (di chuyển sang Reflow)
    stageTransfer.AddTransfer(2, 3, 2, "Di chuyển sang Reflow");
    Console.WriteLine("  CĐ2 → CĐ3: 2 phút (di chuyển)");

    // CĐ3 → CĐ4: 10 phút (chờ nguội sau Reflow!)
    stageTransfer.AddTransfer(3, 4, 10, "Chờ nguội sau Reflow");
    Console.WriteLine("  CĐ3 → CĐ4: 10 phút (chờ nguội sau Reflow)");

    // ═══════════════════════════════════════════════════════════════
    // 6. CẤU HÌNH ROUTING VỚI LEADTIME RIÊNG CHO TỪNG CÔNG ĐOẠN
    //    Mỗi Model có leadtime khác nhau ở từng công đoạn
    // ═══════════════════════════════════════════════════════════════
    Console.WriteLine("\n═══ ROUTING (Leadtime theo Model-Công đoạn) ═══");
    var routingMgr = scheduler.RoutingManager;

    // ─────────────────────────────────────────────────────────────
    // Model A: Leadtime khác nhau cho từng công đoạn
    // CĐ1=0.5, CĐ2=1.2, CĐ3=0.8, CĐ4=0.3 (phút/sp)
    // ─────────────────────────────────────────────────────────────
    routingMgr.SetRoutingWithStageLeadtimes("A", new Dictionary<int, double>
            {
                { 1, 0.5 },  // Solder Paste: 0.5 phút/sp
                { 2, 1.2 },  // Pick & Place: 1.2 phút/sp (lâu nhất)
                { 3, 0.8 },  // Reflow: 0.8 phút/sp
                { 4, 0.3 }   // AOI: 0.3 phút/sp
            });
    Console.WriteLine("  Model A:");
    Console.WriteLine("    CĐ1 Solder Paste = 0.5 ph/sp");
    Console.WriteLine("    CĐ2 Pick & Place = 1.2 ph/sp");
    Console.WriteLine("    CĐ3 Reflow       = 0.8 ph/sp");
    Console.WriteLine("    CĐ4 AOI          = 0.3 ph/sp");

    // ─────────────────────────────────────────────────────────────
    // Model B: Leadtime khác nhau
    // ─────────────────────────────────────────────────────────────
    routingMgr.SetRoutingWithStageLeadtimes("B", new Dictionary<int, double>
            {
                { 1, 0.4 },  // Solder Paste
                { 2, 0.9 },  // Pick & Place
                { 3, 0.6 },  // Reflow
                { 4, 0.25 }  // AOI
            });
    Console.WriteLine("\n  Model B:");
    Console.WriteLine("    CĐ1 = 0.4, CĐ2 = 0.9, CĐ3 = 0.6, CĐ4 = 0.25 ph/sp");

    // ─────────────────────────────────────────────────────────────
    // Model C: Leadtime khác nhau
    // ─────────────────────────────────────────────────────────────
    routingMgr.SetRoutingWithStageLeadtimes("C", new Dictionary<int, double>
            {
                { 1, 0.6 },
                { 2, 1.5 },  // Pick & Place lâu (board phức tạp)
                { 3, 0.9 },
                { 4, 0.4 }
            });
    Console.WriteLine("\n  Model C:");
    Console.WriteLine("    CĐ1 = 0.6, CĐ2 = 1.5, CĐ3 = 0.9, CĐ4 = 0.4 ph/sp");

    // ─────────────────────────────────────────────────────────────
    // Model D: Chỉ 3 công đoạn (bỏ AOI)
    // ─────────────────────────────────────────────────────────────
    routingMgr.SetRoutingWithStageLeadtimes("D", new Dictionary<int, double>
            {
                { 1, 0.3 },
                { 2, 0.7 },
                { 3, 0.5 }
                // Không có CĐ4 (AOI)
            });
    Console.WriteLine("\n  Model D (bỏ AOI):");
    Console.WriteLine("    CĐ1 = 0.3, CĐ2 = 0.7, CĐ3 = 0.5 ph/sp");

    // ─────────────────────────────────────────────────────────────
    // Model E: Leadtime khác nhau
    // ─────────────────────────────────────────────────────────────
    routingMgr.SetRoutingWithStageLeadtimes("E", new Dictionary<int, double>
            {
                { 1, 0.35 },
                { 2, 0.85 },
                { 3, 0.55 },
                { 4, 0.2 }
            });
    Console.WriteLine("\n  Model E:");
    Console.WriteLine("    CĐ1 = 0.35, CĐ2 = 0.85, CĐ3 = 0.55, CĐ4 = 0.2 ph/sp");

    // ═══════════════════════════════════════════════════════════════
    // 7. GIẢI
    // ═══════════════════════════════════════════════════════════════
    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                      ĐANG GIẢI...                         ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

    var result = scheduler.Solve(timeLimitSeconds: 30);

    // ═══════════════════════════════════════════════════════════════
    // 8. HIỂN THỊ KẾT QUẢ
    // ═══════════════════════════════════════════════════════════════
    Console.WriteLine($"\n  Status: {result.Status}");
    Console.WriteLine($"  Thời gian giải: {result.SolveTimeMs} ms");

    if (result.IsSuccess)
    {
        Console.WriteLine($"  Makespan: {result.MakespanMinutes} phút ({result.MakespanMinutes / 60.0:F1} giờ)");

        // In báo cáo tóm tắt
        Console.WriteLine(result.GetSummaryReport());

        // In lịch trình chi tiết
        Console.WriteLine(result.GetDetailedSchedule());

        // In lịch trình đơn giản
        Console.WriteLine(result.GetSimpleSchedule());
    }
    else
    {
        Console.WriteLine("\n  ❌ KHÔNG TÌM ĐƯỢC LỊCH TRÌNH!");
        foreach (var reason in result.FailureReasons)
        {
            Console.WriteLine($"     - {reason}");
        }
    }

    Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                      KẾT THÚC                             ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

    Console.WriteLine("\nNhấn Enter để thoát...");
    Console.ReadLine();

