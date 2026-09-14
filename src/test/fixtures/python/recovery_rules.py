# flagged — error shadowing (high)
def broadScope():
    try:
        work0()
        work1()
        work2()
        work3()
        work4()
        work5()
        work6()
        work7()
    except Failure0:
        work10()

# flagged — recovery dominance (high)
def twoLineProtected():
    try:
        work0()
        work1()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()

# clean — not flagged by recovery dominance
def oneLineProtected():
    try:
        work0()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()

# flagged — recovery dominance (medium)
def workBefore():
    work90()
    try:
        work0()
        work1()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()

# flagged — recovery dominance (medium)
def workAfter():
    try:
        work0()
        work1()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()
    work90()

# flagged — recovery dominance (high)
def multilineLoop():
    try:
        for item in items:
            process(item)
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()

# clean — not flagged by recovery dominance
def commentedTrivial():
    try:
        work0() # mixed

        # only comment
    except Failure0:
        work10()

        # comment only
        work11()
        work12()
        work13()
        work14()

# clean — not flagged by oversized recovery block
def atLimit():
    try:
        work0()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()

# flagged — oversized recovery block (medium)
def overLimit():
    try:
        work0()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()
        work30()

# clean — not flagged by oversized recovery block
def commentedLimit():
    try:
        work0() # mixed

        # only comment
    except Failure0:
        work10()

        # comment only
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()

# clean — not flagged by oversized recovery block
def separateHandlers():
    try:
        work0()
    except Failure0:
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
    except Failure1:
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()

# flagged — oversized recovery block (medium)
def oversizedCleanup():
    try:
        work0()
    finally:
        work10()
        work11()
        work12()
        work13()
        work14()
        work15()
        work16()
        work17()
        work18()
        work19()
        work20()
        work21()
        work22()
        work23()
        work24()
        work25()
        work26()
        work27()
        work28()
        work29()
        work30()

# flagged — oversized recovery block (medium)
def nestedBoundaries():
    try:
        try:
            work()
        except InnerFailure:
            recover_0()
            recover_1()
            recover_2()
            recover_3()
            recover_4()
            recover_5()
            recover_6()
            recover_7()
            recover_8()
            recover_9()
            recover_10()
            recover_11()
            recover_12()
            recover_13()
            recover_14()
            recover_15()
            recover_16()
            recover_17()
            recover_18()
            recover_19()
            recover_20()
    except OuterFailure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()
        recover_8()
        recover_9()
        recover_10()
        recover_11()
        recover_12()
        recover_13()
        recover_14()
        recover_15()
        recover_16()
        recover_17()
        recover_18()
        recover_19()
        recover_20()


# flagged — oversized recovery block (medium)
def nestedFunction():
    def localRecovery():
        try:
            work()
        except Failure:
            recover_0()
            recover_1()
            recover_2()
            recover_3()
            recover_4()
            recover_5()
            recover_6()
            recover_7()
            recover_8()
            recover_9()
            recover_10()
            recover_11()
            recover_12()
            recover_13()
            recover_14()
            recover_15()
            recover_16()
            recover_17()
            recover_18()
            recover_19()
            recover_20()
    localRecovery()


# flagged — oversized recovery block (medium)
def lowRecoveryShare():
    recover_0()
    recover_1()
    recover_2()
    recover_3()
    recover_4()
    recover_5()
    recover_6()
    recover_7()
    recover_8()
    recover_9()
    recover_10()
    recover_11()
    recover_12()
    recover_13()
    recover_14()
    recover_15()
    recover_16()
    recover_17()
    recover_18()
    recover_19()
    recover_20()
    recover_21()
    recover_22()
    recover_23()
    recover_24()
    recover_25()
    recover_26()
    recover_27()
    recover_28()
    recover_29()
    recover_30()
    recover_31()
    recover_32()
    recover_33()
    recover_34()
    recover_35()
    recover_36()
    recover_37()
    recover_38()
    recover_39()
    recover_40()
    recover_41()
    recover_42()
    recover_43()
    recover_44()
    recover_45()
    recover_46()
    recover_47()
    recover_48()
    recover_49()
    try:
        work()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()
        recover_8()
        recover_9()
        recover_10()
        recover_11()
        recover_12()
        recover_13()
        recover_14()
        recover_15()
        recover_16()
        recover_17()
        recover_18()
        recover_19()
        recover_20()


# flagged — oversized recovery block (medium)
def multipleOversized():
    try:
        work()
    except FirstFailure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()
        recover_8()
        recover_9()
        recover_10()
        recover_11()
        recover_12()
        recover_13()
        recover_14()
        recover_15()
        recover_16()
        recover_17()
        recover_18()
        recover_19()
        recover_20()
    except SecondFailure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()
        recover_8()
        recover_9()
        recover_10()
        recover_11()
        recover_12()
        recover_13()
        recover_14()
        recover_15()
        recover_16()
        recover_17()
        recover_18()
        recover_19()
        recover_20()
    finally:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()
        recover_8()
        recover_9()
        recover_10()
        recover_11()
        recover_12()
        recover_13()
        recover_14()
        recover_15()
        recover_16()
        recover_17()
        recover_18()
        recover_19()
        recover_20()


# flagged — oversized recovery block (medium)
def mixedCodeComments():
    try:
        work()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()
        recover_8()
        recover_9()
        recover_10()
        recover_11()
        recover_12()
        recover_13()
        recover_14()
        recover_15()
        recover_16()
        recover_17()
        recover_18()
        recover_19()
        recover_final() # code still counts


# clean — not flagged by recovery dominance
def compactProtected():
    try:
        work(); finish()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()


# flagged — recovery dominance (medium)
def shareHalf():
    try:
        work_0()
        work_1()
        work_2()
        work_3()
        work_4()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()


# flagged — recovery dominance (high)
def shareHigh():
    try:
        work_0()
        work_1()
        work_2()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()


# clean — not flagged by recovery dominance
def belowMinimum():
    try:
        work_0()
        work_1()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()


# flagged — error shadowing (medium)
def broadHalf():
    try:
        work_0()
        work_1()
        work_2()
        work_3()
        work_4()
        work_5()
        work_6()
        work_7()
    except Failure:
        recover_0()
        recover_1()
        recover_2()
        recover_3()
        recover_4()
        recover_5()
        recover_6()
        recover_7()


