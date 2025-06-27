from datetime import datetime, timedelta
from typing import List, Optional, Dict, Any
from enum import Enum
from dataclasses import dataclass
import uuid
from database import Database

class TransactionType(Enum):
    INVOICE_FUNDING = "invoice_funding"
    PAYMENT = "payment"
    FEE_CHARGE = "fee_charge"
    TREASURY_FUNDING = "treasury_funding"
    INVOICE_UPLOAD = "invoice_upload"
    LIMIT_ADJUSTMENT = "limit_adjustment"

class FacilityType(Enum):
    INVOICE_FINANCING = "invoice_financing"
    TRADE_FINANCE = "trade_finance"
    WORKING_CAPITAL = "working_capital"
    TERM_LOAN = "term_loan"

@dataclass
class Transaction:
    id: Optional[int] = None
    type: TransactionType = TransactionType.INVOICE_UPLOAD
    facility_type: FacilityType = FacilityType.INVOICE_FINANCING
    organization_id: int = 0
    invoice_id: Optional[int] = None
    description: str = ""
    amount: float = 0.0
    transaction_date: datetime = None
    maturity_date: datetime = None
    is_paid: bool = False
    payment_date: Optional[datetime] = None
    interest_or_discount_rate: Optional[float] = None
    invoice: Optional[Any] = None  # Would be Invoice object in full implementation
    
    def __post_init__(self):
        if self.transaction_date is None:
            self.transaction_date = datetime.now()
        if self.maturity_date is None:
            self.maturity_date = datetime.min

@dataclass
class AccountStatement:
    id: Optional[int] = None
    organization_id: int = 0
    start_date: datetime = None
    end_date: datetime = None
    generation_date: datetime = None
    opening_balance: float = 0.0
    closing_balance: float = 0.0
    statement_number: str = ""
    transactions: List[Transaction] = None
    organization: Optional[Any] = None  # Would be Organization object in full implementation
    
    def __post_init__(self):
        if self.generation_date is None:
            self.generation_date = datetime.now()
        if self.transactions is None:
            self.transactions = []

class ServiceResult:
    def __init__(self, success: bool = True, message: str = "Operation completed successfully"):
        self.success = success
        self.message = message
    
    @staticmethod
    def successful(message: str = "Operation completed successfully"):
        return ServiceResult(True, message)
    
    @staticmethod
    def failed(message: str):
        return ServiceResult(False, message)

class TransactionService:
    def __init__(self):
        self.db = Database()

    def record_transaction(self, transaction: Transaction) -> Transaction:
        """
        Record a new transaction and create corresponding accounting entries
        
        Args:
            transaction: Transaction object to record
            
        Returns:
            The recorded transaction with assigned ID
        """
        # Assign ID if not present
        if transaction.id is None:
            transaction.id = len(self.db.cursor.execute('SELECT * FROM transactions').fetchall()) + 1
        
        # Add to storage
        self.db.cursor.execute('''
            INSERT INTO transactions (id, type, facility_type, organization_id, invoice_id, description, amount, transaction_date, maturity_date, is_paid, payment_date, interest_or_discount_rate) 
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ''', (transaction.id, transaction.type.value, transaction.facility_type.value, transaction.organization_id, transaction.invoice_id, transaction.description, transaction.amount, transaction.transaction_date.strftime('%Y-%m-%d %H:%M:%S'), transaction.maturity_date.strftime('%Y-%m-%d %H:%M:%S'), transaction.is_paid, transaction.payment_date.strftime('%Y-%m-%d %H:%M:%S') if transaction.payment_date else None, transaction.interest_or_discount_rate))
        self.db.connection.commit()
        
        # Create accounting entries based on transaction type
        try:
            journal_entry = None
            
            if transaction.type == TransactionType.INVOICE_FUNDING:
                if transaction.invoice and transaction.interest_or_discount_rate:
                    discount_amount = transaction.amount * (transaction.interest_or_discount_rate / 100.0)
                    funded_amount = transaction.amount - discount_amount
                    if self._accounting_service:
                        journal_entry = self._accounting_service.create_invoice_funding_entry(
                            transaction, funded_amount, discount_amount)
            
            elif transaction.type == TransactionType.PAYMENT:
                if self._accounting_service:
                    journal_entry = self._accounting_service.create_payment_entry(transaction)
            
            elif transaction.type == TransactionType.FEE_CHARGE:
                if self._accounting_service:
                    journal_entry = self._accounting_service.create_fee_charge_entry(transaction)
            
            elif transaction.type == TransactionType.TREASURY_FUNDING:
                if self._accounting_service:
                    journal_entry = self._accounting_service.create_treasury_funding_entry(transaction)
            
            elif transaction.type in [TransactionType.INVOICE_UPLOAD, TransactionType.LIMIT_ADJUSTMENT]:
                # These transaction types don't require journal entries
                pass
            
            # Auto-post the journal entry for automated transactions
            if journal_entry and self._accounting_service:
                self._accounting_service.post_journal_entry(journal_entry.id, 1)  # Using system user ID 1
                
        except Exception as ex:
            # Log the error but don't fail the transaction
            print(f"Warning: Failed to create accounting entry for transaction {transaction.id}: {ex}")
        
        return transaction
    
    def get_transactions(self, organization_id: int) -> List[Transaction]:
        """
        Get all transactions for a specific organization
        
        Args:
            organization_id: ID of the organization
            
        Returns:
            List of transactions ordered by date (most recent first)
        """
        self.db.cursor.execute('''
            SELECT id, type, facility_type, organization_id, invoice_id, description, amount, transaction_date, maturity_date, is_paid, payment_date, interest_or_discount_rate FROM transactions
            WHERE organization_id = ?
            ORDER BY transaction_date DESC
        ''', (organization_id,))
        rows = self.db.cursor.fetchall()
        transactions = []
        for row in rows:
            transactions.append(Transaction(
                id=row[0],
                type=TransactionType(row[1]),
                facility_type=FacilityType(row[2]),
                organization_id=row[3],
                invoice_id=row[4],
                description=row[5],
                amount=row[6],
                transaction_date=datetime.strptime(row[7], '%Y-%m-%d %H:%M:%S'),
                maturity_date=datetime.strptime(row[8], '%Y-%m-%d %H:%M:%S'),
                is_paid=row[9],
                payment_date=datetime.strptime(row[10], '%Y-%m-%d %H:%M:%S') if row[10] else None,
                interest_or_discount_rate=row[11]
            ))
        return transactions
    
    def generate_account_statement(self, organization_id: int, start_date: datetime, 
                                 end_date: datetime) -> AccountStatement:
        """
        Generate an account statement for a specific organization and date range
        
        Args:
            organization_id: ID of the organization
            start_date: Start date for the statement period
            end_date: End date for the statement period
            
        Returns:
            AccountStatement object with calculated balances
        """
        # Get transactions in the date range
        transactions = [
            t for t in self.get_transactions(organization_id) 
            if (t.organization_id == organization_id and 
                start_date <= t.transaction_date <= end_date)
        ]
        transactions.sort(key=lambda x: x.transaction_date)
        
        # Calculate balances
        opening_balance = 0.0
        closing_balance = 0.0
        
        # Get previous transactions to calculate opening balance
        previous_transactions = [
            t for t in self.get_transactions(organization_id) 
            if (t.organization_id == organization_id and 
                t.transaction_date < start_date)
        ]
        
        for t in previous_transactions:
            if t.type in [TransactionType.INVOICE_FUNDING, TransactionType.FEE_CHARGE]:
                opening_balance -= t.amount
            elif t.type == TransactionType.PAYMENT:
                opening_balance += t.amount
        
        closing_balance = opening_balance
        for t in transactions:
            if t.type in [TransactionType.INVOICE_FUNDING, TransactionType.FEE_CHARGE]:
                closing_balance -= t.amount
            elif t.type == TransactionType.PAYMENT:
                closing_balance += t.amount
        
        # Create statement
        statement = AccountStatement(
            id=len(self._account_statements) + 1,
            organization_id=organization_id,
            start_date=start_date,
            end_date=end_date,
            generation_date=datetime.now(),
            opening_balance=opening_balance,
            closing_balance=closing_balance,
            transactions=transactions,
            statement_number=f"STMT-{datetime.now().year}-{datetime.now().month:02d}-{organization_id:04d}"
        )
        
        self._account_statements.append(statement)
        return statement
    
    def generate_statement_report(self, statement: AccountStatement) -> str:
        """
        Generate a formatted text report for an account statement
        
        Args:
            statement: AccountStatement object
            
        Returns:
            Formatted string report
        """
        lines = []
        
        lines.append("ACCOUNT STATEMENT")
        lines.append("=====================================")
        lines.append(f"Statement No: {statement.statement_number}")
        lines.append(f"Organization: {statement.organization.name if statement.organization else 'N/A'}")
        lines.append(f"Period: {statement.start_date.strftime('%m/%d/%Y')} to {statement.end_date.strftime('%m/%d/%Y')}")
        lines.append(f"Generated: {statement.generation_date.strftime('%m/%d/%Y %H:%M')}\n")
        
        lines.append(f"Opening Balance: ${statement.opening_balance:,.2f}")
        lines.append(f"Closing Balance: ${statement.closing_balance:,.2f}\n")
        
        lines.append("TRANSACTIONS:")
        lines.append("-------------------------------------")
        
        for transaction in statement.transactions:
            if transaction.type == TransactionType.INVOICE_FUNDING:
                effect = "+"  # Funding is a credit to the seller
                description = "CREDIT: " + transaction.description
            elif transaction.type == TransactionType.PAYMENT:
                effect = "+"  # Payment is a credit
                description = "CREDIT: " + transaction.description
            else:
                effect = "-"  # All other transactions are debits
                description = transaction.description
            
            lines.append(f"{transaction.transaction_date.strftime('%m/%d/%Y')} | {description}")
            lines.append(f"  {effect}${transaction.amount:,.2f} | {transaction.type.value} | {transaction.facility_type.value}")
            
            if transaction.invoice_id:
                invoice_num = transaction.invoice.invoice_number if transaction.invoice else f"ID: {transaction.invoice_id}"
                lines.append(f"  Invoice: {invoice_num}")
            
            if transaction.interest_or_discount_rate:
                lines.append(f"  Rate: {transaction.interest_or_discount_rate:.2f}%")
            
            if transaction.maturity_date > datetime.min:
                lines.append(f"  Maturity: {transaction.maturity_date.strftime('%m/%d/%Y')}")
            
            if transaction.is_paid and transaction.payment_date:
                lines.append(f"  Paid on: {transaction.payment_date.strftime('%m/%d/%Y')}")
            
            lines.append("")
        
        lines.append("-------------------------------------")
        lines.append(f"Total Transactions: {len(statement.transactions)}")
        
        return "\n".join(lines)
    
    def record_payment_obligation(self, invoice, amount: float, due_date: datetime) -> Transaction:
        """
        Record a payment obligation for the buyer
        
        Args:
            invoice: Invoice object
            amount: Payment amount
            due_date: Due date for payment
            
        Returns:
            Transaction object representing the payment obligation
        """
        transaction = Transaction(
            type=TransactionType.INVOICE_UPLOAD,  # Using InvoiceUpload type for payment obligation
            facility_type=FacilityType.INVOICE_FINANCING,
            organization_id=getattr(invoice, 'buyer_id', 0) or 0,
            invoice_id=getattr(invoice, 'id', None),
            description=f"Payment obligation for invoice {getattr(invoice, 'invoice_number', 'N/A')}",
            amount=amount,  # Full invoice amount
            transaction_date=datetime.now(),
            maturity_date=due_date,
            is_paid=False
        )
        
        return self.record_transaction(transaction)

# Demo usage and testing
def main():
    """
    Demonstrate the TransactionService functionality
    """
    print("=== Transaction Service Demo ===\n")
    
    # Initialize service
    service = TransactionService()  # Using None for demo
    
    # Create sample transactions
    print("1. Recording sample transactions...")
    
    # Invoice funding transaction
    funding_transaction = Transaction(
        type=TransactionType.INVOICE_FUNDING,
        facility_type=FacilityType.INVOICE_FINANCING,
        organization_id=1,
        invoice_id=101,
        description="Invoice funding for INV-2025-001",
        amount=10000.0,
        transaction_date=datetime(2025, 6, 1),
        maturity_date=datetime(2025, 7, 1),
        interest_or_discount_rate=5.0
    )
    service.record_transaction(funding_transaction)
    
    # Payment transaction
    payment_transaction = Transaction(
        type=TransactionType.PAYMENT,
        facility_type=FacilityType.INVOICE_FINANCING,
        organization_id=1,
        invoice_id=101,
        description="Payment received for INV-2025-001",
        amount=10000.0,
        transaction_date=datetime(2025, 7, 1),
        is_paid=True,
        payment_date=datetime(2025, 7, 1)
    )
    service.record_transaction(payment_transaction)
    
    # Fee charge transaction
    fee_transaction = Transaction(
        type=TransactionType.FEE_CHARGE,
        facility_type=FacilityType.INVOICE_FINANCING,
        organization_id=1,
        description="Processing fee",
        amount=50.0,
        transaction_date=datetime(2025, 6, 1)
    )
    service.record_transaction(fee_transaction)
    
    print(f"Recorded {len(service.db.cursor.execute('SELECT * FROM transactions').fetchall())} transactions")
    
    # Get transactions for organization
    print("\n2. Retrieving transactions for organization 1...")
    org_transactions = service.get_transactions(1)
    for t in org_transactions:
        print(f"  {t.transaction_date.strftime('%m/%d/%Y')} - {t.type.value}: ${t.amount:,.2f}")
    
    # Generate account statement
    print("\n3. Generating account statement...")
    statement = service.generate_account_statement(
        organization_id=1,
        start_date=datetime(2025, 6, 1),
        end_date=datetime(2025, 7, 31)
    )
    
    print(f"Statement generated: {statement.statement_number}")
    print(f"Opening Balance: ${statement.opening_balance:,.2f}")
    print(f"Closing Balance: ${statement.closing_balance:,.2f}")
    
    # Generate statement report
    print("\n4. Generating statement report...")
    report = service.generate_statement_report(statement)
    print("\n" + "="*50)
    print(report)
    print("="*50)
    
    print("\n=== Demo Complete ===")

if __name__ == "__main__":
    main()
