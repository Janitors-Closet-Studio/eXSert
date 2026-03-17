# Sarah Angell
# Program 8
# Internship Event Test Driver

import resumes

def driver():
    print("Testing return_thirtyone()")
    print("Expected return value: 31")
    print("Actual return value:", resumes.return_thirtyone())

    print("\nTesting welcome_message()")
    output = "\"Welcome to the Expo!\""
    print("Expected string:", output)
    print("Expected return value: 36192")
    print("Actual string:", end=" ")
    res = resumes.welcome_message()
    print("Actual return value:", res)

    print("\nTesting get_tables()")
    print("Expected return value: 10")
    print("Actual return value:", resumes.get_tables(150,5))

    print("\nTesting get_people()")
    print("Expected return value: 5")
    print("Actual return value:", resumes.get_people(10,150))

    print("\nTesting get_resumes()")
    print("Expected return value: 150")
    print("Actual return value:", resumes.get_resumes(5,10))
    
    print("\nTesting print_tables()")
    output = "Tables required: 10"
    print("Expected string:", output)
    print("Expected return value: None")
    print("Actual string:", end=" ")
    res = resumes.print_tables(150,5)
    print("Actual return value:", res)

    print("\nTesting print_people()")
    output = "People expected: 5"
    print("Expected string:", output)
    print("Expected return value: None")
    print("Actual string:", end=" ")
    res = resumes.print_people(10,150)
    print("Actual return value:", res)

    print("\nTesting print_resumes()")
    output = "Number of resumes: 150"
    print("Expected string:", output)
    print("Expected return value: None")
    print("Actual string:", end=" ")
    res = resumes.print_resumes(5,10)
    print("Actual return value:", res)

    print("\nTesting get_event_data()")
    print("Expected return value: 150")
    print("Actual return value:", resumes.get_event_data(10,5,0))
    print("Expected return value: 5")
    print("Actual return value:", resumes.get_event_data(10,0,150))
    print("Expected return value: 10")
    print("Actual return value:", resumes.get_event_data(0,5,150))

    print("\nTesting print_event_data()")
    output = "Tables required: 10"
    print("Expected string:", output)
    print("Expected return value: None")
    print("Actual string:", end=" ")
    res = resumes.print_event_data(0,5,150)
    print("Actual return value:", res)

    output = "People expected: 5"
    print("Expected string:", output)
    print("Expected return value: None")
    print("Actual string:", end=" ")
    res = resumes.print_event_data(10,0,150)
    print("Actual return value:", res)
    
    output = "Number of resumes: 150"
    print("Expected string:", output)
    print("Expected return value: None")
    print("Actual string:", end=" ")
    res = resumes.print_event_data(10,5,0)
    print("Actual return value:", res)

driver()
