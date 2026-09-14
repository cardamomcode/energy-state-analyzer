def recoveryDominates():
    try:
        return perform_business_operation()
    except ExpectedFailure as error:
        record_recovery(error)
        notify_recovery(error)
        audit_recovery(error)
        rollback_recovery(error)
        compensate_recovery(error)
        queue_recovery(error)
        tag_recovery(error)
        persist_recovery(error)
        return fallback_recovery(error)
    except UnexpectedFailure as error:
        record_recovery(error)
        notify_recovery(error)
        audit_recovery(error)
        rollback_recovery(error)
        compensate_recovery(error)
        queue_recovery(error)
        tag_recovery(error)
        persist_recovery(error)
        return fallback_recovery(error)


def broadBoundary():
    try:
        step_one()
        step_two()
        step_three()
        step_four()
        step_five()
        step_six()
        step_seven()
        step_eight()
        step_nine()
        step_ten()
    except ExpectedFailure:
        recover()


def combinedBoundary():
    try:
        step_one()
        step_two()
        step_three()
        step_four()
        step_five()
        step_six()
        step_seven()
        step_eight()
    except ExpectedFailure as error:
        recover_one(error)
        recover_two(error)
        recover_three(error)
        recover_four(error)
        recover_five(error)
        recover_six(error)
        recover_seven(error)
        recover_eight(error)


def separateBoundaries():
    try:
        first_one()
        first_two()
        first_three()
        first_four()
        first_five()
        first_six()
        first_seven()
        first_eight()
    except FirstFailure:
        recover_first()

    try:
        second_one()
        second_two()
        second_three()
        second_four()
        second_five()
        second_six()
        second_seven()
        second_eight()
    except SecondFailure:
        recover_second()
