-module(error_boundary).

clean_boundary(X) ->
    try work(X)
    catch
        error:Reason -> recover(Reason)
    end.

broad_boundary(X) ->
    try
        step_a(X),
        step_b(X),
        step_c(X),
        step_d(X),
        step_e(X),
        step_f(X),
        step_g(X),
        step_h(X)
    catch
        error:Reason -> recover(Reason)
    end.

recovery_heavy(X) ->
    try
        prepare(X),
        work(X)
    catch
        error:Reason ->
            log_a(Reason),
            log_b(Reason),
            log_c(Reason),
            log_d(Reason),
            recover(Reason)
    end.

oversized_recovery(X) ->
    try work(X)
    catch
        error:Reason ->
            log_01(Reason),
            log_02(Reason),
            log_03(Reason),
            log_04(Reason),
            log_05(Reason),
            log_06(Reason),
            log_07(Reason),
            log_08(Reason),
            log_09(Reason),
            log_10(Reason),
            log_11(Reason),
            log_12(Reason),
            log_13(Reason),
            log_14(Reason),
            log_15(Reason),
            log_16(Reason),
            log_17(Reason),
            log_18(Reason),
            log_19(Reason),
            log_20(Reason),
            recover(Reason)
    end.
