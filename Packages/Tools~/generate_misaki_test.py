import dataclasses
import json

from misaki import en

A_TEST_CASES = [
    "‘Hello’",
    "‘Test’ and ‘Example’",
    "«Bonjour»",
    "«Test «nested» quotes»",
    "(Hello)",
    "(Nested (Parentheses))",
    "こんにちは、世界！",
    "これはテストです：はい？",
    "Hello World",
    "Hello   World",
    "Hello\n   \nWorld",
    "Dr. Smith",
    "DR. Brown",
    "Mr. Smith",
    "MR. Anderson",
    "Ms. Taylor",
    "MS. Carter",
    "Mrs. Johnson",
    "MRS. Wilson",
    "Apples, oranges, etc.",
    "Apples, etc. Pears.",
    "Yeah",
    "yeah",
    "1990",
    "12:34",
    "2022s",
    "1,000",
    "12,345,678",
    "$100",
    "£1.50",
    "12.34",
    "0.01",
    "10-20",
    "5-10",
    "10S",
    "5S",
    "Cat's tail",
    "X's mark",
    "U.S.A.",
    "A.B.C",
]

B_TEST_CASES = [
    "‘Hello’",
    "‘Test’ and ‘Example’",
    "«Bonjour»",
    "«Test «nested» quotes»",
    "(Hello)",
    "(Nested (Parentheses))",
    "こんにちは、世界！",
    "これはテストです：はい？",
    "Hello World",
    "Hello   World",
    "Hello\n   \nWorld",
    "Dr. Smith",
    "DR. Brown",
    "Mr. Smith",
    "MR. Anderson",
    "Ms. Taylor",
    "MS. Carter",
    "Mrs. Johnson",
    "Apples, oranges, etc.",
    "Apples, etc. Pears.",
    "1990",
    "12:34",
    "1,000",
    "12,345,678",
    "$100",
    "£1.50",
    "12.34",
    "0.01",
    "Cat's tail",
    "X's mark",
]


def token_to_dict(token):
    """
    Convert a token to a dictionary.

    Args:
        token (object): Token object.

    Returns:
        dict: Dictionary representation of the token.
    """
    d = dataclasses.asdict(token)
    d.pop("_", None)
    return d


def make_test_json(test_data, g2p, jsonFilePath):
    """
    Generate test data for g2p and save it to a JSON file.

    Args:
        test_data (list): List of test cases.
        g2p (object): g2p object.
        jsonFilePath (str): Path to save the JSON file.
    """
    results = []
    for test in test_data:
        phonemes, tokens = g2p(test)
        # convert tokens dataclass to dict
        # dict_tokens = [token_to_dict(token) for token in tokens]
        results.append({"text": test, "phonemes": phonemes})

    wrapper = {
        "data": results,
    }

    with open(jsonFilePath, "w", encoding="utf-8") as f:
        json.dump(wrapper, f, ensure_ascii=False, indent=2)


g2pA = en.G2P(trf=False, british=False, fallback=None)
g2pB = en.G2P(trf=False, british=True, fallback=None)

testDataDir = "../com.github.asus4.kokoro-tts/Tests/Data"
pathA = f"{testDataDir}/american_test_data.json"
pathB = f"{testDataDir}/british_test_data.json"

make_test_json(A_TEST_CASES, g2pA, pathA)
make_test_json(B_TEST_CASES, g2pB, pathB)
print("Finished generating test data.")
