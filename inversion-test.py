def process_user_data(user_data):
    """Example of a function that could benefit from inversion"""
    if user_data and user_data.get("active"):
        if user_data.get("email"):
            if user_data.get("permissions"):
                if user_data["permissions"].get("read"):
                    # This whole function is wrapped in nested validations!
                    result = perform_complex_processing(user_data)
                    send_notification(user_data["email"])
                    log_activity(user_data["id"])
                    update_last_seen(user_data["id"])
                    return result
    return None


def validate_order(order):
    """Another inversion opportunity - single large if dominating function"""
    if order and order.get("items") and len(order["items"]) > 0:
        # 80% of the function logic is inside this one if-statement
        total = 0
        for item in order["items"]:
            if item.get("price") and item.get("quantity"):
                total += item["price"] * item["quantity"]

        if total > 0:
            order["total"] = total
            order["status"] = "validated"
            save_order(order)
            send_confirmation_email(order["customer_email"])
            update_inventory(order["items"])
            log_order_validation(order["id"])
            return True

    return False


def deeply_nested_logic(data):
    """Deep if-nesting that could be flattened"""

    if data:
        if data.get("type") == "premium":
            if data.get("subscription"):
                if data["subscription"].get("active"):
                    if data["subscription"].get("expires") > today():
                        # Very deep nesting - hard to follow
                        return process_premium_user(data)
    return None


# Better version using guard clauses (inversion):
def process_user_data_better(user_data):
    """Example of good inversion - early returns"""
    if not user_data or not user_data.get("active"):
        return None

    if not user_data.get("email"):
        return None

    if not user_data.get("permissions") or not user_data["permissions"].get("read"):
        return None

    # Main logic is no longer nested!
    result = perform_complex_processing(user_data)
    send_notification(user_data["email"])
    log_activity(user_data["id"])
    update_last_seen(user_data["id"])
    return result


def perform_complex_processing(data):
    return {"processed": True}


def send_notification(email):
    pass


def log_activity(user_id):
    pass


def update_last_seen(user_id):
    pass


def save_order(order):
    pass


def send_confirmation_email(email):
    pass


def update_inventory(items):
    pass


def log_order_validation(order_id):
    pass


def process_premium_user(data):
    return {"premium": True}


def today():
    from datetime import date

    return date.today()
