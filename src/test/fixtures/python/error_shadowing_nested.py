def outer():
    def shadowed_inner():
        try:
            first_risky_operation()
            second_risky_operation()
            third_risky_operation()
            fourth_risky_operation()
            fifth_risky_operation()
        except ExpectedFailure:
            recover_expected_failure()
        except UnexpectedFailure:
            recover_unexpected_failure()

    return ordinary_result()
