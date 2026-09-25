using System;
using System.Collections.Generic;

namespace CommonIR.Generators.Binary.WASM.Model
{
    internal static class WasmOpCodeComplexity
    {
        private const int LocalSlots = 16;              
        private const int LocalSlotsLog2 = 4;
        private const int RegBits = 64;                 
        private const int AddrBits = 32;        

        private const int FullAdder = 5;
        private const int HalfAdder = 2;
        private const int Mux2 = 3;
        private const int Mux4 = 9;


        private static int Add(int n) => n * FullAdder;
        private static int Sub(int n) => n * (FullAdder + 1);
        private static int Mul(int n) => n * (n + 1) / 2 + (n * (n - 1) / 2) * FullAdder;
        private static int DivCore(int n) => n * (Sub(n) + n * Mux2);
        private static int CondNeg(int n) => n + n * HalfAdder;              
        private static int NonZero(int n) => n - 1;                  
        private static int DivU(int n) => DivCore(n) + NonZero(n);
        private static int DivS(int n) => DivCore(n) + 3 * CondNeg(n) + 1 + NonZero(n) + 2 * (n - 1) + 1;                            
        private static int RemU(int n) => DivCore(n) + NonZero(n);
        private static int RemS(int n) => DivCore(n) + 2 * CondNeg(n) + NonZero(n); 
        private static int Eqz(int n) => n;                                  
        private static int Eq(int n) => 2 * n - 1;                           
        private static int Ne(int n) => Eq(n) + 1;
        private static int LtU(int n) => 5 * n;
        private static int LtS(int n) => 5 * n + 3;                          
        private static int LeU(int n) => LtU(n) + 1;
        private static int LeS(int n) => LtS(n) + 1;


        private const int F32Add = 1400, F64Add = 3200;
        private const int F32Mul = 3700, F64Mul = 17200;
        private const int F32Div = 5700, F64Div = 24600;
        private const int F32Sqrt = 5200, F64Sqrt = 23700;

        private const int F32Nan = 70, F64Nan = 130;                         
        private const int F32BothZero = 62, F64BothZero = 126;       
        private static int FEq(int n, int nan, int zero) => Eq(n) + zero + nan + 2;
        private static int FNe(int n, int nan, int zero) => FEq(n, nan, zero) + 1;
        private static int FLt(int n, int nan, int zero) => 5 * n + 10 + nan + zero;
        private static int FLe(int n, int nan, int zero) => FLt(n, nan, zero) + Eq(n) + 1;
        private static int FMinMax(int n, int nan) => 5 * n + 10 + nan + n * Mux2 + 10;


        private static readonly int MemBase = Add(AddrBits) + 5 * AddrBits;           
        private static readonly int Wide = AddrBits * HalfAdder;               
        private static readonly int ByteSelect = 8 * Mux4;            
        private static readonly int HalfSelect = 16 * Mux2;           
        private static readonly int SignSel8 = Mux4;                  
        private static readonly int SignSel16 = Mux2;                 
        private const int WriteEnable8 = 6;                           
        private const int WriteEnable16 = 3;


        private static readonly int RegRead = (LocalSlots - 1) * Mux2 * RegBits;                               
        private static readonly int RegWrite = LocalSlots * Mux2 * RegBits + LocalSlots * 3 + LocalSlotsLog2; 

        private static readonly int PcLoad = 32 * Mux2;       

        public static readonly IReadOnlyDictionary<WasmOpCodes, int> GateCount =
            new Dictionary<WasmOpCodes, int>
            {
                [WasmOpCodes.Unreachable] = 1,                                                   
                [WasmOpCodes.Nop] = 0,
                [WasmOpCodes.Block] = 0,                                                         
                [WasmOpCodes.Loop] = 0,                                                          
                [WasmOpCodes.If] = NonZero(32) + PcLoad,                                         
                [WasmOpCodes.Else] = PcLoad,                                                     
                [WasmOpCodes.End] = 0,                                           
                [WasmOpCodes.Call] = PcLoad + Add(32) * 0 + 32 * HalfAdder,                      
                [WasmOpCodes.Br] = PcLoad,
                [WasmOpCodes.Br_if] = NonZero(32) + PcLoad,
                [WasmOpCodes.Br_table] = LtU(32) + Add(32) + PcLoad,                             
                [WasmOpCodes.Return] = PcLoad + 32 * HalfAdder,                  

                [WasmOpCodes.Memory_size] = 0,                                                   
                [WasmOpCodes.Memory_grow] = Add(32) + LtU(32) + 32 * Mux2,       

                [WasmOpCodes.I32_store] = MemBase,
                [WasmOpCodes.I64_store] = MemBase + Wide,
                [WasmOpCodes.F32_store] = MemBase,
                [WasmOpCodes.F64_store] = MemBase + Wide,
                [WasmOpCodes.I32_store8] = MemBase + WriteEnable8,
                [WasmOpCodes.I32_store16] = MemBase + WriteEnable16,
                [WasmOpCodes.I64_store8] = MemBase + WriteEnable8,
                [WasmOpCodes.I64_store16] = MemBase + WriteEnable16,
                [WasmOpCodes.I64_store32] = MemBase,

                [WasmOpCodes.I32_load] = MemBase,
                [WasmOpCodes.I64_load] = MemBase + Wide,
                [WasmOpCodes.F32_load] = MemBase,
                [WasmOpCodes.F64_load] = MemBase + Wide,
                [WasmOpCodes.I32_load8_s] = MemBase + ByteSelect + SignSel8,
                [WasmOpCodes.I32_load8_u] = MemBase + ByteSelect,
                [WasmOpCodes.I32_load16_s] = MemBase + HalfSelect + SignSel16,
                [WasmOpCodes.I32_load16_u] = MemBase + HalfSelect,
                [WasmOpCodes.I64_load8_s] = MemBase + ByteSelect + SignSel8,
                [WasmOpCodes.I64_load8_u] = MemBase + ByteSelect,
                [WasmOpCodes.I64_load16_s] = MemBase + HalfSelect + SignSel16,
                [WasmOpCodes.I64_load16_u] = MemBase + HalfSelect,
                [WasmOpCodes.I64_load32_s] = MemBase,                                            
                [WasmOpCodes.I64_load32_u] = MemBase,                            
                [WasmOpCodes.Local_get] = RegRead,
                [WasmOpCodes.Local_set] = RegWrite,
                [WasmOpCodes.Local_tee] = RegWrite,                              
                [WasmOpCodes.Global_get] = RegRead,
                [WasmOpCodes.Global_set] = RegWrite,

                [WasmOpCodes.I32_const] = 5 * 7 * Mux2,                                          
                [WasmOpCodes.I64_const] = 10 * 7 * Mux2,                                         
                [WasmOpCodes.F32_const] = 0,
                [WasmOpCodes.F64_const] = 0,

                [WasmOpCodes.I32_add] = Add(32),
                [WasmOpCodes.I32_sub] = Sub(32),
                [WasmOpCodes.I32_mul] = Mul(32),
                [WasmOpCodes.I32_div_s] = DivS(32),
                [WasmOpCodes.I32_div_u] = DivU(32),
                [WasmOpCodes.I32_rem_s] = RemS(32),
                [WasmOpCodes.I32_rem_u] = RemU(32),

                [WasmOpCodes.I64_add] = Add(64),
                [WasmOpCodes.I64_sub] = Sub(64),
                [WasmOpCodes.I64_mul] = Mul(64),
                [WasmOpCodes.I64_div_s] = DivS(64),
                [WasmOpCodes.I64_div_u] = DivU(64),
                [WasmOpCodes.I64_rem_s] = RemS(64),
                [WasmOpCodes.I64_rem_u] = RemU(64),

                [WasmOpCodes.F32_abs] = 1,                                                       
                [WasmOpCodes.F32_neg] = 1,                                                       
                [WasmOpCodes.F32_sqrt] = F32Sqrt,
                [WasmOpCodes.F32_add] = F32Add,
                [WasmOpCodes.F32_sub] = F32Add + 1,                                              
                [WasmOpCodes.F32_mul] = F32Mul,
                [WasmOpCodes.F32_div] = F32Div,
                [WasmOpCodes.F32_min] = FMinMax(32, F32Nan),
                [WasmOpCodes.F32_max] = FMinMax(32, F32Nan),
                [WasmOpCodes.F32_copysign] = Mux2,                               
                [WasmOpCodes.F64_abs] = 1,
                [WasmOpCodes.F64_neg] = 1,
                [WasmOpCodes.F64_sqrt] = F64Sqrt,
                [WasmOpCodes.F64_add] = F64Add,
                [WasmOpCodes.F64_sub] = F64Add + 1,
                [WasmOpCodes.F64_mul] = F64Mul,
                [WasmOpCodes.F64_div] = F64Div,
                [WasmOpCodes.F64_min] = FMinMax(64, F64Nan),
                [WasmOpCodes.F64_max] = FMinMax(64, F64Nan),
                [WasmOpCodes.F64_copysign] = Mux2,

                [WasmOpCodes.I32_eqz] = Eqz(32),
                [WasmOpCodes.I32_eq] = Eq(32),
                [WasmOpCodes.I32_ne] = Ne(32),
                [WasmOpCodes.I32_lt_s] = LtS(32),
                [WasmOpCodes.I32_lt_u] = LtU(32),
                [WasmOpCodes.I32_gt_s] = LtS(32),                                                
                [WasmOpCodes.I32_gt_u] = LtU(32),
                [WasmOpCodes.I32_le_s] = LeS(32),
                [WasmOpCodes.I32_le_u] = LeU(32),
                [WasmOpCodes.I32_ge_s] = LeS(32),
                [WasmOpCodes.I32_ge_u] = LeU(32),

                [WasmOpCodes.I64_eqz] = Eqz(64),
                [WasmOpCodes.I64_eq] = Eq(64),
                [WasmOpCodes.I64_ne] = Ne(64),
                [WasmOpCodes.I64_lt_s] = LtS(64),
                [WasmOpCodes.I64_lt_u] = LtU(64),
                [WasmOpCodes.I64_gt_s] = LtS(64),
                [WasmOpCodes.I64_gt_u] = LtU(64),
                [WasmOpCodes.I64_le_s] = LeS(64),
                [WasmOpCodes.I64_le_u] = LeU(64),
                [WasmOpCodes.I64_ge_s] = LeS(64),
                [WasmOpCodes.I64_ge_u] = LeU(64),

                [WasmOpCodes.F32_eq] = FEq(32, F32Nan, F32BothZero),
                [WasmOpCodes.F32_ne] = FNe(32, F32Nan, F32BothZero),
                [WasmOpCodes.F32_lt] = FLt(32, F32Nan, F32BothZero),
                [WasmOpCodes.F32_gt] = FLt(32, F32Nan, F32BothZero),
                [WasmOpCodes.F32_le] = FLe(32, F32Nan, F32BothZero),
                [WasmOpCodes.F32_ge] = FLe(32, F32Nan, F32BothZero),

                [WasmOpCodes.F64_eq] = FEq(64, F64Nan, F64BothZero),
                [WasmOpCodes.F64_ne] = FNe(64, F64Nan, F64BothZero),
                [WasmOpCodes.F64_lt] = FLt(64, F64Nan, F64BothZero),
                [WasmOpCodes.F64_gt] = FLt(64, F64Nan, F64BothZero),
                [WasmOpCodes.F64_le] = FLe(64, F64Nan, F64BothZero),
                [WasmOpCodes.F64_ge] = FLe(64, F64Nan, F64BothZero),
            };

        public static int Of(WasmOpCodes op) => GateCount[op];

        public static void Validate()
        {
            foreach (WasmOpCodes op in Enum.GetValues(typeof(WasmOpCodes)))
            {
                if (!GateCount.ContainsKey(op))
                    throw new InvalidOperationException($"Missing gate count for {op}");
            }
        }
    }
}